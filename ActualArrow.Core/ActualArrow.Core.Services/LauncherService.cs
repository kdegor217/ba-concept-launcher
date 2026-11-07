using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Abstractions;
using ActualArrow.Core.Models;
using ActualArrow.Core.Services.Install;
using ActualArrow.Core.Services.Runner;

namespace ActualArrow.Core.Services;

public sealed class LauncherService : ILauncherService
{
	private enum ReleaseCheck
	{
		None,
		Full,
		ForPlay
	}

	private sealed class Operation(CancellationToken token)
	{
		public CancellationToken Token { get; } = token;

		public StatusMessage? Status { get; set; }

		public Banner? Banner { get; set; }

		public LauncherState? Idle { get; set; }

		public string? InstallTag { get; set; }

		public bool InstallIsRollback { get; set; }

		public Action? AfterCompletion { get; set; }
	}

	private sealed class GameRunningException : LauncherException
	{
		public GameRunningException()
			: base("Status.CloseGame", "The game is running", null)
		{
		}
	}

	private static readonly TimeSpan ProgressThrottle = TimeSpan.FromMilliseconds(100L);

	private static readonly JsonSerializerOptions ManifestJson = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly CommandLineOptions _options;

	private readonly IAppPaths _paths;

	private readonly ISettingsStore _settings;

	private readonly IGameLocator _locator;

	private readonly IReleaseFeed _feed;

	private readonly IDownloader _downloader;

	private readonly IPackInstaller _pack;

	private readonly IMelonLoaderInstaller _melon;

	private readonly IProxyManager _proxies;

	private readonly IGameRunner _runner;

	private readonly IUserPrompts _prompts;

	private readonly ILog _log;

	private readonly TimeProvider _time;

	private readonly IComparer<string?> _tags;

	private readonly object _gate = new object();

	private LauncherState _state = new LauncherState();

	private int _busy;

	private CancellationTokenSource? _operationCts;

	private CancellationTokenSource? _watchCts;

	private ReleaseFeedResult? _lastRelease;

	private bool _initialized;

	private long _lastProgressStamp;

	private OperationKind? _lastProgressKind;

	private int _lastSteamSeconds = -1;

	private long? _vanillaStartedStamp;

	private long _lastReleaseStamp;

	private bool _startupStashDone;

	private string? _lastGameLine;

	public LauncherState State => Volatile.Read(in _state);

	internal Task BackgroundWatch { get; private set; } = Task.CompletedTask;

	internal TimeSpan ModLogTimeout { get; set; } = Brand.Limits.ModLogWatch;

	internal TimeSpan FreshCheckAge { get; set; } = Brand.GitHub.FreshCheckAge;

	internal TimeSpan PlayCheckTimeout { get; set; } = Brand.GitHub.PlayCheckTimeout;

	internal TimeSpan VanillaStartGuard { get; set; } = Brand.Limits.VanillaStartGuard;

	internal TimeSpan GameExitPoll { get; set; } = TimeSpan.FromSeconds(1L);

	internal Task SettingsRefresh { get; private set; } = Task.CompletedTask;

	internal Task ExitWatch { get; private set; } = Task.CompletedTask;

	public event EventHandler<LauncherState>? StateChanged;

	public event EventHandler<ModLogOutcome>? ModLogWatchEnded;

	public LauncherService(CommandLineOptions options, IAppPaths paths, ISettingsStore settings, IGameLocator locator, IReleaseFeed feed, IDownloader downloader, IInstallRecordStore records, IPackInstaller pack, IMelonLoaderInstaller melonLoader, IProxyManager proxies, IGameRunner runner, IUserPrompts prompts, ILog log, TimeProvider? time = null, IComparer<string?>? tagComparer = null)
	{
		_options = options;
		_paths = paths;
		_settings = settings;
		_locator = locator;
		_feed = feed;
		_downloader = downloader;
		_pack = pack;
		_melon = melonLoader;
		_proxies = proxies;
		_runner = runner;
		_prompts = prompts;
		_log = log;
		_time = time ?? TimeProvider.System;
		_tags = tagComparer ?? VersionComparer.Instance;
	}

	public Task<OperationResult> RefreshAsync(CancellationToken ct = default(CancellationToken))
	{
		return RunAsync("refresh", OperationKind.CheckingRelease, async (Operation op) =>
		{
			if (!_startupStashDone)
			{
				_startupStashDone = true;
				RefreshSnapshot refreshSnapshot = await SnapshotAsync(ReleaseCheck.None, null).ConfigureAwait(continueOnCapturedContext: false);
				if (refreshSnapshot.Game?.IsValid ?? false)
				{
					await StashLoaderCoreAsync(refreshSnapshot.Game.GameDir, "launcher start", op.Token).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
			RefreshSnapshot refreshSnapshot2 = await SnapshotAsync(ReleaseCheck.Full, op).ConfigureAwait(continueOnCapturedContext: false);
			op.Idle = StateBuilder.Build(refreshSnapshot2, _tags);
			GameLocation? game = refreshSnapshot2.Game;
			if ((object)game == null || !game.IsValid)
			{
				return new OperationResult(OperationOutcome.Failed, StatusMessage.Of("Notice.GameNotFound"));
			}
			ReleaseFeedError releaseFeedError = refreshSnapshot2.Release?.Error;
			if ((object)releaseFeedError != null)
			{
				PackHealth? health = refreshSnapshot2.Health;
				if ((object)health == null || !health.IsInstalled)
				{
					return new OperationResult(OperationOutcome.Failed, StateBuilder.ErrorMessage(releaseFeedError));
				}
			}
			return OperationResult.Ok;
		}, ct);
	}

	public Task<OperationResult> PlayModdedAsync(CancellationToken ct = default(CancellationToken))
	{
		return RunAsync("play-modded", OperationKind.CheckingRelease, async (Operation op) =>
		{
			EnsureNoVanillaStartPending();
			RefreshSnapshot snap = await RequireGameAsync(op, ReleaseCheck.ForPlay).ConfigureAwait(continueOnCapturedContext: false);
			string gameDir = snap.Game.GameDir;
			EnsureGameClosed();
			Banner notice = null;
			PackRecord record = snap.Health?.Record?.Pack;
			bool installedHere = (object)record != null && TagText.SameRepo(record.Repo, snap.Repo);
			ReleaseFeedError releaseFeedError = snap.Release?.Error;
			if ((object)releaseFeedError != null)
			{
				if (!installedHere)
				{
					throw ToException(StateBuilder.ErrorMessage(releaseFeedError), releaseFeedError.Detail);
				}
				notice = new Banner(BannerSeverity.Warning, StatusMessage.Of("Notice.UsingInstalled", record.Tag), StateBuilder.ErrorMessage(releaseFeedError));
				_log.Warn($"Release check failed ({releaseFeedError.GetType().Name}: {releaseFeedError.Detail}); using installed {record.Tag}");
			}
			if (!(await EnsureMelonLoaderAsync(gameDir, snap.MelonLoader, force: false, op).ConfigureAwait(continueOnCapturedContext: false)))
			{
				return OperationResult.Cancelled;
			}
			PackAction packAction = StateBuilder.DecidePack(snap.Health, snap.Release, snap.Repo, _tags);
			switch (packAction)
			{
			case PackAction.Blocked:
			{
				bool flag = !installedHere;
				if (!flag)
				{
					flag = !(await TryReinstallFromLocalCopyAsync(gameDir, record, op).ConfigureAwait(continueOnCapturedContext: false));
				}
				if (flag)
				{
					ReleaseFeedError releaseFeedError2 = snap.Release?.Error;
					throw ToException(((object)releaseFeedError2 != null) ? StateBuilder.ErrorMessage(releaseFeedError2) : StatusMessage.Of("Error.DownloadFailed"), "installed files are damaged and no release is available");
				}
				break;
			}
			default:
				await InstallReleaseAsync(gameDir, snap.Repo, snap.Release.Release, (object)record == null, packAction == PackAction.Repair, packAction == PackAction.Rollback, op).ConfigureAwait(continueOnCapturedContext: false);
				break;
			case PackAction.None:
				break;
			}
			PackRecord packRecord = (await _pack.CheckQuickAsync(gameDir, op.Token).ConfigureAwait(continueOnCapturedContext: false))?.Record?.Pack ?? (installedHere ? record : null);
			if ((object)packRecord != null)
			{
				ModManagerCompat.Write(gameDir, packRecord, _log);
			}
			EnsureGameClosed();
			EnsureNoVanillaStartPending();
			if (await _proxies.RestoreMelonProxyAsync(gameDir, op.Token).ConfigureAwait(continueOnCapturedContext: false))
			{
				_log.Info("Restored stashed version.dll");
			}
			if (!_proxies.GetState(gameDir).InRoot.Any((string n) => string.Equals(n, "version.dll", StringComparison.OrdinalIgnoreCase)))
			{
				_log.Warn("version.dll missing after restore: reinstalling MelonLoader");
				MelonLoaderStatus status = await _melon.GetStatusAsync(gameDir, op.Token).ConfigureAwait(continueOnCapturedContext: false);
				if (!(await EnsureMelonLoaderAsync(gameDir, status, force: true, op).ConfigureAwait(continueOnCapturedContext: false)))
				{
					return OperationResult.Cancelled;
				}
				if (!_proxies.GetState(gameDir).InRoot.Any((string n) => string.Equals(n, "version.dll", StringComparison.OrdinalIgnoreCase)))
				{
					throw new LauncherException("Error.MelonLoaderFailed", "version.dll is not in the game root", null);
				}
			}
			EnsureGameClosed();
			if (!(await WaitForSteamAsync(op).ConfigureAwait(continueOnCapturedContext: false)))
			{
				op.Banner = new Banner(BannerSeverity.Error, StatusMessage.Of("Error.SteamNotStarted"));
				return new OperationResult(OperationOutcome.Failed, StatusMessage.Of("Error.SteamNotStarted"));
			}
			Step(op, OperationKind.Launching, StatusMessage.Of("Status.Launching"));
			bool firstStart = Brand.Game.IsFirstModdedStart(gameDir);
			LaunchResult launch = await _runner.LaunchModdedAsync(gameDir, op.Token).ConfigureAwait(continueOnCapturedContext: false);
			op.Banner = (firstStart ? new Banner(BannerSeverity.Warning, StatusMessage.Of("Notice.FirstModdedStart")) : notice);
			if (launch.DryRun)
			{
				op.Status = StatusMessage.Of("Status.DryRun", launch.Description);
				return new OperationResult(OperationOutcome.Succeeded, op.Status);
			}
			op.Status = StatusMessage.Of("Status.LaunchedModded");
			op.AfterCompletion = () =>
			{
				StartModLogWatch(gameDir, launch.StartedUtc);
			};
			return new OperationResult(OperationOutcome.Succeeded, op.Status, GameStarted: true);
		}, ct);
	}

	public Task<OperationResult> PlayVanillaAsync(CancellationToken ct = default(CancellationToken))
	{
		return RunAsync("play-vanilla", OperationKind.Launching, async (Operation op) =>
		{
			RefreshSnapshot snap = await RequireGameAsync(op, ReleaseCheck.None).ConfigureAwait(continueOnCapturedContext: false);
			string gameDir = snap.Game.GameDir;
			EnsureGameClosed();
			Step(op, OperationKind.Launching, StatusMessage.Of("Status.Launching"));
			CancelModLogWatch();
			try
			{
				IReadOnlyList<string> readOnlyList = await _proxies.StashAllAsync(gameDir, op.Token).ConfigureAwait(continueOnCapturedContext: false);
				if (readOnlyList.Count > 0)
				{
					_log.Info("Stashed before the anti-cheat start: " + string.Join(", ", readOnlyList));
				}
			}
			catch (IOException ex)
			{
				IReadOnlyList<string> inRoot = _proxies.GetState(gameDir).InRoot;
				throw new LauncherException("Error.ProxyLeft", "could not stash proxies: " + ex.Message, ex, string.Join(", ", (inRoot.Count > 0) ? inRoot : Brand.Game.ProxyDllNames));
			}
			ProxyState state = _proxies.GetState(gameDir);
			if (state.AnyInRoot)
			{
				throw new LauncherException("Error.ProxyLeft", "proxy still in the game root after stashing", null, string.Join(", ", state.InRoot));
			}
			IReadOnlyList<string> readOnlyList2 = LoaderScan.FindOtherLoaders(gameDir, _log);
			if (readOnlyList2.Count > 0)
			{
				throw new LauncherException("Error.ForeignLoader", "mod loader under another name in the root: " + string.Join(", ", readOnlyList2), null, string.Join(", ", readOnlyList2));
			}
			if (_runner.VanillaThroughSteam && !SameDirectory(snap.DetectedGameDir, gameDir))
			{
				throw new LauncherException("Error.NotSteamFolder", "vanilla goes through Steam, whose install is " + (snap.DetectedGameDir ?? "unknown") + ", not " + gameDir, null);
			}
			if (!File.Exists(Path.Combine(gameDir, "EACLauncher.exe")) && !SameDirectory(snap.DetectedGameDir, gameDir))
			{
				throw new LauncherException("Error.NoAntiCheatLauncher", $"{"EACLauncher.exe"} is missing in {gameDir} and Steam's install is {snap.DetectedGameDir ?? "unknown"}", null);
			}
			if (!(await WaitForSteamAsync(op).ConfigureAwait(continueOnCapturedContext: false)))
			{
				op.Banner = new Banner(BannerSeverity.Error, StatusMessage.Of("Error.SteamNotStarted"));
				return new OperationResult(OperationOutcome.Failed, StatusMessage.Of("Error.SteamNotStarted"));
			}
			Step(op, OperationKind.Launching, StatusMessage.Of("Status.Launching"));
			LaunchResult launchResult;
			try
			{
				launchResult = await _runner.LaunchVanillaAsync(gameDir, op.Token).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (InvalidOperationException ex2)
			{
				throw new LauncherException("Error.LaunchFailed", ex2.Message, ex2);
			}
			if (launchResult.DryRun)
			{
				op.Status = StatusMessage.Of("Status.DryRun", launchResult.Description);
				return new OperationResult(OperationOutcome.Succeeded, op.Status);
			}
			lock (_gate)
			{
				_vanillaStartedStamp = _time.GetTimestamp();
			}
			op.Status = StatusMessage.Of("Status.LaunchedVanilla");
			return new OperationResult(OperationOutcome.Succeeded, op.Status, GameStarted: true);
		}, ct);
	}

	public Task<OperationResult> VerifyFilesAsync(CancellationToken ct = default(CancellationToken))
	{
		return RunAsync("verify", OperationKind.Verifying, async (Operation op) =>
		{
			RefreshSnapshot snap = await RequireGameAsync(op, ReleaseCheck.None).ConfigureAwait(continueOnCapturedContext: false);
			string gameDir = snap.Game.GameDir;
			PackRecord record = snap.Health?.Record?.Pack ?? throw new LauncherException("Error.NotInstalled", "verify without an installed pack", null);
			EnsureGameClosed();
			Step(op, OperationKind.Verifying, StatusMessage.Of("Status.Verifying"));
			PackVerifyResult packVerifyResult = await _pack.VerifyAsync(gameDir, ProgressFor(op), op.Token).ConfigureAwait(continueOnCapturedContext: false);
			if (packVerifyResult.IsOk)
			{
				_log.Info($"Verify: {packVerifyResult.Checked} files OK");
				op.Status = StatusMessage.Of("Status.VerifyOk");
				return new OperationResult(OperationOutcome.Succeeded, op.Status);
			}
			int damaged = packVerifyResult.Missing.Count + packVerifyResult.Changed.Count;
			_log.Warn($"Verify: {packVerifyResult.Missing.Count} missing, {packVerifyResult.Changed.Count} changed: {string.Join(", ", packVerifyResult.Missing.Concat(packVerifyResult.Changed).Take(20))}");
			Step(op, OperationKind.CheckingRelease, StatusMessage.Of("Status.Checking"));
			ReleaseFeedResult feed = (_lastRelease = await _feed.GetLatestAsync(snap.Repo, op.Token).ConfigureAwait(continueOnCapturedContext: false));
			ReleaseInfo latest = StateBuilder.ReleaseFor(feed, snap.Repo)?.Release;
			bool sameRepo = TagText.SameRepo(record.Repo, snap.Repo);
			if ((((object)latest != null) & sameRepo) && TagText.Same(latest.Tag, record.Tag))
			{
				await InstallReleaseAsync(gameDir, snap.Repo, latest, firstInstall: false, force: true, rollback: false, op).ConfigureAwait(continueOnCapturedContext: false);
			}
			else if (!(await TryReinstallFromLocalCopyAsync(gameDir, record, op).ConfigureAwait(continueOnCapturedContext: false)))
			{
				if ((object)latest == null)
				{
					ReleaseFeedError error = feed.Error;
					throw ToException(((object)error != null) ? StateBuilder.ErrorMessage(error) : StatusMessage.Of("Error.DownloadFailed"), "no source to repair from");
				}
				await InstallReleaseAsync(gameDir, snap.Repo, latest, firstInstall: false, force: true, sameRepo && _tags.Compare(latest.Tag, record.Tag) < 0, op).ConfigureAwait(continueOnCapturedContext: false);
			}
			op.Status = StatusMessage.Of("Status.VerifyRepaired", damaged);
			return new OperationResult(OperationOutcome.Succeeded, op.Status);
		}, ct);
	}

	public Task<OperationResult> UninstallAsync(CancellationToken ct = default(CancellationToken))
	{
		return RunAsync("uninstall", OperationKind.Uninstalling, async (Operation op) =>
		{
			string gameDir = (await RequireGameAsync(op, ReleaseCheck.None).ConfigureAwait(continueOnCapturedContext: false)).Game.GameDir;
			EnsureGameClosed();
			UninstallPlan uninstallPlan = await _pack.GetUninstallPlanAsync(gameDir, op.Token).ConfigureAwait(continueOnCapturedContext: false);
			if (uninstallPlan.IsEmpty)
			{
				op.Status = StatusMessage.Of("Status.NothingToUninstall");
				return new OperationResult(OperationOutcome.Succeeded, op.Status);
			}
			UninstallOptions uninstallOptions = await _prompts.ConfirmUninstallAsync(uninstallPlan, op.Token).ConfigureAwait(continueOnCapturedContext: false);
			if ((object)uninstallOptions == null)
			{
				return OperationResult.Cancelled;
			}
			EnsureGameClosed();
			Step(op, OperationKind.Uninstalling, StatusMessage.Of("Status.Uninstalling"));
			ModManagerCompat.Remove(gameDir, _log);
			UninstallResult uninstallResult = await _pack.UninstallAsync(gameDir, uninstallOptions, ProgressFor(op), op.Token).ConfigureAwait(continueOnCapturedContext: false);
			_log.Info($"Uninstalled: {uninstallResult.RemovedItems} items, backup {uninstallResult.BackupDir ?? "-"}");
			IReadOnlyList<string> inRoot = _proxies.GetState(gameDir).InRoot;
			if (inRoot.Count > 0)
			{
				_log.Error("Proxy still in the game root after uninstall: " + string.Join(", ", inRoot));
				op.Status = StatusMessage.Of("Error.ProxyLeft", string.Join(", ", inRoot));
				op.Banner = new Banner(BannerSeverity.Error, op.Status);
				return new OperationResult(OperationOutcome.Failed, op.Status);
			}
			op.Status = StatusMessage.Of("Status.Uninstalled");
			return new OperationResult(OperationOutcome.Succeeded, op.Status);
		}, ct);
	}

	public Task<OperationResult> ReinstallMelonLoaderAsync(CancellationToken ct = default(CancellationToken))
	{
		return RunAsync("reinstall-melonloader", OperationKind.InstallingMelonLoader, async (Operation op) =>
		{
			EnsureNoVanillaStartPending();
			RefreshSnapshot refreshSnapshot = await RequireGameAsync(op, ReleaseCheck.None).ConfigureAwait(continueOnCapturedContext: false);
			EnsureGameClosed();
			if (!(await EnsureMelonLoaderAsync(refreshSnapshot.Game.GameDir, refreshSnapshot.MelonLoader, force: true, op).ConfigureAwait(continueOnCapturedContext: false)))
			{
				return OperationResult.Cancelled;
			}
			op.Status = StatusMessage.Of("Status.Ready");
			return new OperationResult(OperationOutcome.Succeeded, op.Status);
		}, ct);
	}

	public void CancelCurrentOperation()
	{
		CancellationTokenSource cancellationTokenSource = Volatile.Read(in _operationCts);
		if (cancellationTokenSource == null)
		{
			return;
		}
		try
		{
			_log.Info("Cancel requested");
			cancellationTokenSource.Cancel();
		}
		catch (ObjectDisposedException)
		{
		}
	}

	public async Task<SettingsApplyResult> ApplySettingsAsync(SettingsChange change, CancellationToken ct = default(CancellationToken))
	{
		List<StatusMessage> list = new List<StatusMessage>();
		string gameDir = null;
		if (!string.IsNullOrWhiteSpace(change.GameDir))
		{
			try
			{
				gameDir = Path.GetFullPath(change.GameDir.Trim());
			}
			catch (Exception ex) when ((ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException) ? true : false)
			{
				gameDir = null;
			}
			if (gameDir == null || SafeValidate(gameDir) != GameDirValidation.Valid)
			{
				list.Add(StatusMessage.Of("Settings.GameDirInvalid"));
			}
		}
		string repo = null;
		if (!string.IsNullOrWhiteSpace(change.PackRepo))
		{
			repo = change.PackRepo.Trim();
			if (!Regex.IsMatch(repo, "^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})/[A-Za-z0-9._-]{1,100}$"))
			{
				list.Add(StatusMessage.Of("Settings.RepoInvalid"));
			}
		}
		if (list.Count > 0)
		{
			return new SettingsApplyResult(list);
		}
		string language = Brand.Languages.FirstOrDefault((string l) => string.Equals(l, change.Language?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "ru";
		await _settings.UpdateAsync((LauncherSettings s) => s with
		{
			GameDir = gameDir,
			PackRepo = repo,
			Language = language,
			CloseOnLaunch = change.CloseOnLaunch
		}, ct).ConfigureAwait(continueOnCapturedContext: false);
		_log.Info($"Settings saved: gameDir={gameDir ?? "(auto)"} repo={repo ?? "(default)"} lang={language} closeOnLaunch={change.CloseOnLaunch}");
		SettingsRefresh = Task.Run(async () =>
		{
			try
			{
				if ((await RefreshAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false)).Outcome == OperationOutcome.Busy)
				{
					_log.Info("Settings saved while busy: the state is recomputed when the running operation ends");
				}
			}
			catch (Exception exception)
			{
				_log.Warn("Refresh after saving the settings failed", exception);
			}
		});
		return SettingsApplyResult.Saved;
	}

	public async Task<bool> StashLoaderIfIdleAsync(CancellationToken ct = default(CancellationToken))
	{
		if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
		{
			return false;
		}
		try
		{
			GameLocation game = State.Game;
			if ((object)game == null || !game.IsValid)
			{
				return false;
			}
			return await StashLoaderCoreAsync(game.GameDir, "launcher exit", ct).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_log.Warn("Could not set the mod loader aside", ex);
			return false;
		}
		finally
		{
			Volatile.Write(ref _busy, 0);
		}
	}

	public bool IsGameRunning()
	{
		try
		{
			return _runner.IsGameRunning();
		}
		catch (Exception)
		{
			return false;
		}
	}

	public async Task<string?> GameDirToWatchAsync(CancellationToken ct = default(CancellationToken))
	{
		try
		{
			GameLocation game = State.Game;
			if ((object)game == null || !game.IsValid || !_runner.IsGameRunning())
			{
				return null;
			}
			MelonLoaderStatus melonLoaderStatus = await _melon.GetStatusAsync(game.GameDir, ct).ConfigureAwait(continueOnCapturedContext: false);
			return (melonLoaderStatus.RecordedTag != null && melonLoaderStatus.ProxyInRoot) ? game.GameDir : null;
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_log.Warn("Could not check whether the game needs an exit watcher", ex);
			return null;
		}
	}

	public async Task<bool> WatchGameAndStashAsync(string gameDir, Func<bool> launcherOpen, CancellationToken ct = default(CancellationToken))
	{
		if (_locator.Validate(gameDir) != GameDirValidation.Valid)
		{
			_log.Warn("Exit watcher: " + gameDir + " is not a game folder");
			return false;
		}
		_log.Info("Exit watcher: waiting for the game to exit (" + gameDir + ")");
		int absent = 0;
		while (true)
		{
			if (launcherOpen())
			{
				_log.Info("Exit watcher: the launcher is open again, it takes over");
				return false;
			}
			absent = ((!_runner.IsGameRunning()) ? (absent + 1) : 0);
			if (absent >= 2)
			{
				break;
			}
			await Task.Delay(GameExitPoll, _time, ct).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
		{
			return false;
		}
		try
		{
			return await StashLoaderCoreAsync(gameDir, "the game exited after the launcher closed", ct).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_log.Warn("Exit watcher could not set the mod loader aside", ex);
			return false;
		}
		finally
		{
			Volatile.Write(ref _busy, 0);
		}
	}

	public void DismissBanner()
	{
		Publish((LauncherState s) => s with
		{
			Banner = null
		});
	}

	public void OpenGameFolder()
	{
		GameLocation game = State.Game;
		if ((object)game != null && game.IsValid)
		{
			OpenSafely(game.GameDir);
		}
		else
		{
			_log.Info("Open game folder: no valid game folder");
		}
	}

	public void OpenModLog()
	{
		GameLocation game = State.Game;
		if ((object)game == null || !game.IsValid)
		{
			_log.Info("Open mod log: no valid game folder");
			return;
		}
		string text = ToLocal(game.GameDir, "MelonLoader/Latest.log");
		if (File.Exists(text))
		{
			OpenSafely(text);
		}
		else
		{
			_log.Info("Open mod log: " + text + " does not exist");
		}
	}

	public void OpenLauncherLog()
	{
		string text = _log.FilePath ?? _paths.LogFile;
		if (File.Exists(text))
		{
			OpenSafely(text);
		}
		else if (Directory.Exists(_paths.Root))
		{
			OpenSafely(_paths.Root);
		}
	}

	private async Task<OperationResult> RunAsync(string name, OperationKind firstStep, Func<Operation, Task<OperationResult>> body, CancellationToken ct)
	{
		if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
		{
			_log.Info("Rejected " + name + ": another operation is running");
			return OperationResult.Busy;
		}
		using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		Volatile.Write(ref _operationCts, cts);
		Operation op = new Operation(cts.Token);
		lock (_gate)
		{
			_lastProgressKind = null;
			_lastSteamSeconds = -1;
		}
		Publish((LauncherState s) => s with
		{
			CurrentOperation = firstStep,
			Progress = null,
			CanCancel = true,
			Banner = null,
			Status = ((firstStep == OperationKind.CheckingRelease) ? StatusMessage.Of("Status.Checking") : s.Status)
		});
		_log.Info("Operation " + name + " started");
		OperationResult result;
		try
		{
			await EnsureInitializedAsync(op.Token).ConfigureAwait(continueOnCapturedContext: false);
			result = await body(op).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (cts.IsCancellationRequested)
		{
			_log.Info("Operation " + name + " cancelled");
			op.Status = StatusMessage.Of("Status.Cancelled");
			op.Banner = null;
			result = OperationResult.Cancelled;
		}
		catch (LauncherException ex2) when (((Func<bool>)delegate
		{
			// Could not convert BlockContainer to single expression
			bool flag = ex2 is GameRunningException;
			if (!flag)
			{
				string messageKey = ex2.MessageKey;
				flag = ((messageKey == "Status.CloseGame" || messageKey == "Status.AntiCheatStarting") ? true : false);
			}
			return flag;
		}).Invoke())
		{
			_log.Info("Operation " + name + " refused: " + ((ex2.MessageKey == "Status.AntiCheatStarting") ? "a vanilla start is under way" : "the game is running"));
			op.Status = StatusMessage.Of((ex2.MessageKey == "Status.AntiCheatStarting") ? "Status.AntiCheatStarting" : "Status.CloseGame");
			op.Banner = new Banner(BannerSeverity.Warning, op.Status);
			result = new OperationResult(OperationOutcome.Failed, op.Status);
		}
		catch (LauncherException ex3)
		{
			_log.Error($"Operation {name} failed: {ex3.MessageKey}: {ex3.Message}", ex3);
			StatusMessage message = ex3.ToStatusMessage();
			op.Banner = new Banner(BannerSeverity.Error, message);
			result = new OperationResult(OperationOutcome.Failed, message);
		}
		catch (Exception ex4)
		{
			_log.Error("Operation " + name + " failed unexpectedly", ex4);
			StatusMessage message2 = StatusMessage.Of("Error.Unexpected", ex4.Message);
			op.Banner = new Banner(BannerSeverity.Error, message2);
			result = new OperationResult(OperationOutcome.Failed, message2);
		}
		LauncherState idle = op.Idle;
		if ((object)idle == null)
		{
			try
			{
				idle = StateBuilder.Build(await SnapshotAsync(ReleaseCheck.None, null).ConfigureAwait(continueOnCapturedContext: false), _tags);
			}
			catch (Exception exception)
			{
				_log.Warn("Could not recompute the state after " + name, exception);
			}
		}
		if (result.Outcome == OperationOutcome.Cancelled && (object)op.Status == null)
		{
			op.Status = StatusMessage.Of("Status.Cancelled");
		}
		Publish((LauncherState s) =>
		{
			LauncherState obj = idle ?? s;
			return obj with
			{
				CurrentOperation = null,
				Progress = null,
				CanCancel = false,
				Status = (op.Status ?? (((object)idle == null) ? s.Status : null) ?? StatusMessage.Of("Status.Ready")),
				Banner = (op.Banner ?? idle?.Banner)
			};
		});
		Volatile.Write(ref _operationCts, null);
		Volatile.Write(ref _busy, 0);
		_log.Info($"Operation {name} ended: {result.Outcome}{(((object)result.Message == null) ? "" : (" " + result.Message))}");
		if (result.Outcome == OperationOutcome.Succeeded)
		{
			op.AfterCompletion?.Invoke();
		}
		return result;
	}

	private async Task EnsureInitializedAsync(CancellationToken ct)
	{
		if (_initialized)
		{
			return;
		}
		try
		{
			_paths.EnsureCreated();
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			_log.Warn("Could not create the app-data folders under " + _paths.Root, ex);
		}
		await _settings.LoadAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		foreach (string item in _options.Unknown)
		{
			_log.Warn("Unknown command-line argument ignored: " + item);
		}
		if (_options.DryRun)
		{
			_log.Info("Dry run: no process will be started");
		}
		_initialized = true;
	}

	private void Publish(Func<LauncherState, LauncherState> change)
	{
		lock (_gate)
		{
			Volatile.Write(ref _state, change(_state));
		}
		try
		{
			StateChanged?.Invoke(this, Volatile.Read(in _state));
		}
		catch (Exception exception)
		{
			_log.Warn("A StateChanged handler threw", exception);
		}
	}

	private void Step(Operation op, OperationKind kind, StatusMessage status)
	{
		lock (_gate)
		{
			_lastProgressKind = kind;
		}
		Publish((LauncherState s) => s with
		{
			CurrentOperation = kind,
			Progress = new OperationProgress(kind),
			Status = status
		});
	}

	private IProgress<OperationProgress> ProgressFor(Operation op)
	{
		return new ActualArrow.Core.Services.Runner.InlineProgress<OperationProgress>((OperationProgress p) =>
		{
			ReportProgress(op, p);
		});
	}

	private void ReportProgress(Operation op, OperationProgress p)
	{
		lock (_gate)
		{
			long timestamp = _time.GetTimestamp();
			double? fraction = p.Fraction;
			bool flag = fraction.HasValue && fraction.GetValueOrDefault() >= 1.0;
			if (_lastProgressKind == p.Kind && !flag && _time.GetElapsedTime(_lastProgressStamp, timestamp) < ProgressThrottle)
			{
				return;
			}
			_lastProgressKind = p.Kind;
			_lastProgressStamp = timestamp;
		}
		StatusMessage status = StatusFor(op, p);
		Publish((LauncherState s) => s with
		{
			CurrentOperation = p.Kind,
			Progress = p,
			Status = (status ?? s.Status)
		});
	}

	private static StatusMessage? StatusFor(Operation op, OperationProgress p)
	{
		return p.Kind switch
		{
			OperationKind.DownloadingPack => StatusMessage.Of("Status.DownloadingPack", p.BytesDone.GetValueOrDefault(), p.BytesTotal.GetValueOrDefault()), 
			OperationKind.DownloadingMelonLoader => StatusMessage.Of("Status.DownloadingMelonLoader", p.BytesDone.GetValueOrDefault(), p.BytesTotal ?? 20155622), 
			OperationKind.InstallingMelonLoader => StatusMessage.Of("Status.InstallingMelonLoader"), 
			OperationKind.InstallingPack => op.InstallIsRollback ? StatusMessage.Of("Status.RollingBack", op.InstallTag ?? "") : StatusMessage.Of("Status.InstallingPack", op.InstallTag ?? ""), 
			OperationKind.Verifying => StatusMessage.Of("Status.Verifying"), 
			OperationKind.Uninstalling => StatusMessage.Of("Status.Uninstalling"), 
			OperationKind.CheckingRelease => StatusMessage.Of("Status.Checking"), 
			OperationKind.Launching => StatusMessage.Of("Status.Launching"), 
			_ => null, 
		};
	}

	private void EnsureGameClosed()
	{
		if (_runner.IsGameRunning())
		{
			throw new GameRunningException();
		}
	}

	private void EnsureNoVanillaStartPending()
	{
		long? vanillaStartedStamp;
		lock (_gate)
		{
			vanillaStartedStamp = _vanillaStartedStamp;
		}
		if (vanillaStartedStamp.HasValue)
		{
			long valueOrDefault = vanillaStartedStamp.GetValueOrDefault();
			if (_time.GetElapsedTime(valueOrDefault) < VanillaStartGuard)
			{
				throw new LauncherException("Status.AntiCheatStarting", "a vanilla start is under way", null);
			}
		}
	}

	private async Task<bool> StashLoaderCoreAsync(string gameDir, string why, CancellationToken ct)
	{
		if (_runner.IsGameRunning())
		{
			return false;
		}
		MelonLoaderStatus melonLoaderStatus = await _melon.GetStatusAsync(gameDir, ct).ConfigureAwait(continueOnCapturedContext: false);
		if (melonLoaderStatus.RecordedTag == null || !melonLoaderStatus.ProxyInRoot)
		{
			return false;
		}
		if (!(await _proxies.StashMelonProxyAsync(gameDir, ct).ConfigureAwait(continueOnCapturedContext: false)))
		{
			return false;
		}
		_log.Info("version.dll set aside (" + why + "); the next modded start puts it back");
		return true;
	}

	private async Task<RefreshSnapshot> RequireGameAsync(Operation op, ReleaseCheck check)
	{
		RefreshSnapshot refreshSnapshot = await SnapshotAsync(check, op).ConfigureAwait(continueOnCapturedContext: false);
		GameLocation? game = refreshSnapshot.Game;
		if ((object)game == null || !game.IsValid)
		{
			throw new LauncherException("Error.GameDirInvalid", $"no valid game folder ({refreshSnapshot.Game?.GameDir ?? "none"}: {refreshSnapshot.Game?.Validation})", null);
		}
		return refreshSnapshot;
	}

	private async Task<RefreshSnapshot> SnapshotAsync(ReleaseCheck check, Operation? op)
	{
		CancellationToken ct = op?.Token ?? CancellationToken.None;
		LauncherSettings settings = _settings.Current;
		string repo = (string.IsNullOrWhiteSpace(_options.Repo) ? settings.EffectivePackRepo : _options.Repo.Trim());
		string detected = null;
		try
		{
			detected = await _locator.DetectAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_log.Warn("Game auto-detection failed", ex);
		}
		GameLocation game = ResolveGame(settings, detected);
		LogGameDir(game);
		PackHealth health = null;
		MelonLoaderStatus melon = null;
		ProxyState proxies = null;
		bool modLog = false;
		if (game?.IsValid ?? false)
		{
			health = await _pack.CheckQuickAsync(game.GameDir, ct).ConfigureAwait(continueOnCapturedContext: false);
			melon = await _melon.GetStatusAsync(game.GameDir, ct).ConfigureAwait(continueOnCapturedContext: false);
			proxies = _proxies.GetState(game.GameDir);
			modLog = File.Exists(ToLocal(game.GameDir, "MelonLoader/Latest.log"));
		}
		if (check == ReleaseCheck.ForPlay)
		{
			ReleaseFeedResult releaseFeedResult = FreshRelease(repo);
			if ((object)releaseFeedResult != null)
			{
				_log.Info($"Using the release check from {_time.GetElapsedTime(_lastReleaseStamp).TotalSeconds:0} s ago: {releaseFeedResult.Release.Tag}");
				goto IL_05b0;
			}
		}
		TimeSpan? timeSpan;
		if (check != ReleaseCheck.None)
		{
			if (op != null)
			{
				Step(op, OperationKind.CheckingRelease, StatusMessage.Of("Status.Checking"));
			}
			if (check == ReleaseCheck.ForPlay)
			{
				PackRecord packRecord = health?.Record?.Pack;
				if ((object)packRecord != null && health.IsIntact && TagText.SameRepo(packRecord.Repo, repo))
				{
					timeSpan = PlayCheckTimeout;
					goto IL_0409;
				}
			}
			timeSpan = null;
			goto IL_0409;
		}
		goto IL_05b0;
		IL_0409:
		TimeSpan? cap = timeSpan;
		ReleaseFeedResult releaseFeedResult2 = await CheckReleaseAsync(repo, cap, ct).ConfigureAwait(continueOnCapturedContext: false);
		lock (_gate)
		{
			_lastRelease = releaseFeedResult2;
			_lastReleaseStamp = _time.GetTimestamp();
		}
		ReleaseFeedError error = releaseFeedResult2.Error;
		if ((object)error != null)
		{
			_log.Warn($"Release check for {repo} failed: {error.GetType().Name} {error.Detail}");
		}
		else
		{
			_log.Info($"Latest release of {repo}: {releaseFeedResult2.Release.Tag}{(releaseFeedResult2.FromCache ? " (cached, 304)" : "")}");
		}
		goto IL_05b0;
		IL_05b0:
		return new RefreshSnapshot(game, detected, repo, health, melon, proxies, StateBuilder.ReleaseFor(_lastRelease, repo), ReadCachedManifest(), File.Exists(_paths.CachedPreviewFile) ? _paths.CachedPreviewFile : null, modLog);
	}

	private ReleaseFeedResult? FreshRelease(string repo)
	{
		lock (_gate)
		{
			ReleaseFeedResult lastRelease = _lastRelease;
			if ((object)lastRelease == null || (object)lastRelease.Error != null || (object)lastRelease.Release == null || (object)StateBuilder.ReleaseFor(lastRelease, repo) == null)
			{
				return null;
			}
			return (_lastReleaseStamp != 0L && _time.GetElapsedTime(_lastReleaseStamp) < FreshCheckAge) ? lastRelease : null;
		}
	}

	private async Task<ReleaseFeedResult> CheckReleaseAsync(string repo, TimeSpan? cap, CancellationToken ct)
	{
		if (!cap.HasValue)
		{
			return await _feed.GetLatestAsync(repo, ct).ConfigureAwait(continueOnCapturedContext: false);
		}
		using CancellationTokenSource capped = CancellationTokenSource.CreateLinkedTokenSource(ct);
		capped.CancelAfter(cap.Value);
		try
		{
			return await _feed.GetLatestAsync(repo, capped.Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			_log.Warn($"GitHub did not answer within {cap.Value.TotalSeconds:0} s; playing the installed version");
			return ReleaseFeedResult.Fail(new ReleaseFeedError.Offline($"no answer within {cap.Value.TotalSeconds:0} s"));
		}
	}

	private void LogGameDir(GameLocation? game)
	{
		string text = (((object)game == null) ? "none" : $"{game.GameDir} ({game.Source}, {game.Validation})");
		lock (_gate)
		{
			if (text == _lastGameLine)
			{
				return;
			}
			_lastGameLine = text;
		}
		_log.Info("Game folder: " + text);
	}

	private static bool SameDirectory(string? a, string? b)
	{
		if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
		{
			return false;
		}
		StringComparison comparisonType = ((OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
		return string.Equals(Path.TrimEndingDirectorySeparator(FullPathOrSelf(a)), Path.TrimEndingDirectorySeparator(FullPathOrSelf(b)), comparisonType);
	}

	private GameLocation? ResolveGame(LauncherSettings settings, string? detected)
	{
		if (!string.IsNullOrWhiteSpace(_options.GameDir))
		{
			string text = FullPathOrSelf(_options.GameDir);
			return new GameLocation(text, GameDirSource.CommandLine, SafeValidate(text));
		}
		GameLocation gameLocation = null;
		if (!string.IsNullOrWhiteSpace(settings.GameDir))
		{
			string text2 = FullPathOrSelf(settings.GameDir);
			gameLocation = new GameLocation(text2, GameDirSource.Settings, SafeValidate(text2));
			if (gameLocation.IsValid)
			{
				return gameLocation;
			}
		}
		if (!string.IsNullOrWhiteSpace(detected))
		{
			GameLocation gameLocation2 = new GameLocation(FullPathOrSelf(detected), GameDirSource.AutoDetected, SafeValidate(detected));
			if (gameLocation2.IsValid || (object)gameLocation == null)
			{
				return gameLocation2;
			}
		}
		return gameLocation;
	}

	private GameDirValidation SafeValidate(string dir)
	{
		try
		{
			return _locator.Validate(dir);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_log.Warn("Validating " + dir + " failed", ex);
			return GameDirValidation.NotFound;
		}
	}

	private async Task<bool> EnsureMelonLoaderAsync(string gameDir, MelonLoaderStatus? status, bool force, Operation op)
	{
		if (!force && (status?.IsReady ?? false))
		{
			return true;
		}
		bool replace = false;
		if ((object)status != null && status.State == MelonLoaderState.UnrecordedFolder)
		{
			replace = await _prompts.ConfirmReplaceUnrecordedMelonLoaderAsync(ToLocal(gameDir, "MelonLoader"), op.Token).ConfigureAwait(continueOnCapturedContext: false);
			if (!replace)
			{
				_log.Info("User kept the unrecorded MelonLoader folder: operation cancelled");
				return false;
			}
		}
		Step(op, OperationKind.DownloadingMelonLoader, StatusMessage.Of("Status.DownloadingMelonLoader", 0L, 20155622L));
		try
		{
			await _melon.InstallAsync(gameDir, replace, ProgressFor(op), op.Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (InstallException ex) when (ex.Failure == InstallFailure.UnrecordedMelonLoader && !replace)
		{
			if (!(await _prompts.ConfirmReplaceUnrecordedMelonLoaderAsync(ToLocal(gameDir, "MelonLoader"), op.Token).ConfigureAwait(continueOnCapturedContext: false)))
			{
				return false;
			}
			await _melon.InstallAsync(gameDir, replaceUnrecordedFolder: true, ProgressFor(op), op.Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		_log.Info("MelonLoader v0.7.3 installed");
		return true;
	}

	private async Task InstallReleaseAsync(string gameDir, string repo, ReleaseInfo release, bool firstInstall, bool force, bool rollback, Operation op)
	{
		_paths.EnsureCreated();
		ReleaseAsset asset = release.Asset;
		string destination = Path.Combine(_paths.DownloadsDir, SafeFileName(asset.Name));
		string expected = asset.Sha256;
		bool flag = expected != null;
		if (flag)
		{
			flag = await FileHash.MatchesAsync(destination, asset.Size, expected, op.Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		string sha;
		if (flag)
		{
			_log.Info("Reusing verified download " + destination);
			sha = expected.ToLowerInvariant();
		}
		else
		{
			Step(op, OperationKind.DownloadingPack, StatusMessage.Of("Status.DownloadingPack", 0L, asset.Size));
			long size = asset.Size;
			ActualArrow.Core.Services.Runner.InlineProgress<DownloadProgress> progress = new ActualArrow.Core.Services.Runner.InlineProgress<DownloadProgress>((DownloadProgress d) =>
			{
				ReportProgress(op, new OperationProgress(OperationKind.DownloadingPack, d.BytesDone, d.BytesTotal.GetValueOrDefault(size)));
			});
			DownloadResult downloadResult = await _downloader.DownloadAsync(new DownloadRequest(asset.DownloadUrl, destination, asset.Size, asset.Sha256), progress, op.Token).ConfigureAwait(continueOnCapturedContext: false);
			destination = downloadResult.Path;
			sha = downloadResult.Sha256;
			_log.Info($"Downloaded {asset.Name} ({downloadResult.Size} bytes)");
		}
		await InstallPackFileAsync(gameDir, destination, repo, release.Tag, asset.Name, sha, firstInstall, force, rollback, op).ConfigureAwait(continueOnCapturedContext: false);
		PruneDownloads(Path.GetFileName(destination));
	}

	private void PruneDownloads(string keep)
	{
		try
		{
			foreach (string item in Directory.EnumerateFiles(_paths.DownloadsDir))
			{
				string fileName = Path.GetFileName(item);
				if ((fileName.EndsWith(".bamod", StringComparison.OrdinalIgnoreCase) || (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && !fileName.Equals("MelonLoader.x64.zip", StringComparison.OrdinalIgnoreCase))) && !fileName.Equals(keep, StringComparison.OrdinalIgnoreCase))
				{
					File.Delete(item);
					_log.Info("Old download removed: " + fileName);
				}
			}
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			_log.Debug("Could not prune downloads: " + ex.Message);
		}
	}

	private async Task InstallPackFileAsync(string gameDir, string packPath, string repo, string tag, string assetName, string sha, bool firstInstall, bool force, bool rollback, Operation op)
	{
		PackInspection packInspection = await _pack.InspectAsync(packPath, op.Token).ConfigureAwait(continueOnCapturedContext: false);
		foreach (SkippedEntry item in packInspection.Skipped)
		{
			_log.Warn($"Pack entry skipped: {item.EntryName} ({item.Reason})");
		}
		IReadOnlyList<string> foreign = Array.Empty<string>();
		ForeignModsAction foreignAction = ForeignModsAction.Keep;
		if (firstInstall)
		{
			foreign = await _pack.FindForeignModsAsync(gameDir, packInspection, op.Token).ConfigureAwait(continueOnCapturedContext: false);
			if (foreign.Count > 0)
			{
				List<string> modNames = foreign.Select(ForeignDisplayName).ToList();
				bool flag = await _prompts.ConfirmBackupForeignModsAsync(modNames, op.Token).ConfigureAwait(continueOnCapturedContext: false);
				foreignAction = (flag ? ForeignModsAction.MoveToBackup : ForeignModsAction.Keep);
				_log.Info($"Not in the pack ({string.Join(", ", foreign)}): {foreignAction}");
				if (!flag && foreign.Any((string f) => f.StartsWith("UserData/", StringComparison.OrdinalIgnoreCase)))
				{
					_log.Warn("Extra UserData files were kept: mods that fingerprint their data (ArrowQueue) will report a different build than other players");
				}
			}
		}
		EnsureGameClosed();
		op.InstallTag = tag;
		op.InstallIsRollback = rollback;
		Step(op, OperationKind.InstallingPack, rollback ? StatusMessage.Of("Status.RollingBack", tag) : StatusMessage.Of("Status.InstallingPack", tag));
		PackInstallResult packInstallResult = await _pack.InstallAsync(new PackInstallRequest
		{
			GameDir = gameDir,
			PackPath = packPath,
			Repo = repo,
			Tag = tag,
			AssetName = assetName,
			AssetSha256 = sha,
			ForeignMods = foreign,
			ForeignModsAction = foreignAction,
			ForceRewrite = force
		}, ProgressFor(op), op.Token).ConfigureAwait(continueOnCapturedContext: false);
		_log.Info($"Pack {tag} installed: +{packInstallResult.Added} ~{packInstallResult.Changed} -{packInstallResult.Removed} ={packInstallResult.Unchanged}, backup {packInstallResult.BackupDir ?? "-"}");
		op.Status = StatusMessage.Of("Status.Installed", tag);
	}

	private async Task<bool> TryReinstallFromLocalCopyAsync(string gameDir, PackRecord record, Operation op)
	{
		string path = Path.Combine(_paths.DownloadsDir, SafeFileName(record.AssetName));
		if (!(await FileHash.MatchesAsync(path, null, record.AssetSha256, op.Token).ConfigureAwait(continueOnCapturedContext: false)))
		{
			_log.Info("No verified local copy of " + record.AssetName + " to repair from");
			return false;
		}
		_log.Info("Repairing " + record.Tag + " from the local copy " + path);
		await InstallPackFileAsync(gameDir, path, record.Repo, record.Tag, record.AssetName, record.AssetSha256, firstInstall: false, force: true, rollback: false, op).ConfigureAwait(continueOnCapturedContext: false);
		return true;
	}

	private async Task<bool> WaitForSteamAsync(Operation op)
	{
		Step(op, OperationKind.WaitingForSteam, StatusMessage.Of("Status.WaitingForSteam", 0));
		int total = (int)Brand.Steam.StartTimeout.TotalSeconds;
		ActualArrow.Core.Services.Runner.InlineProgress<TimeSpan> waited = new ActualArrow.Core.Services.Runner.InlineProgress<TimeSpan>((TimeSpan timeSpan) =>
		{
			int seconds = (int)timeSpan.TotalSeconds;
			lock (_gate)
			{
				if (seconds == _lastSteamSeconds)
				{
					return;
				}
				_lastSteamSeconds = seconds;
			}
			Publish((LauncherState s) =>
			{
				int? itemsDone = Math.Min(seconds, total);
				int? itemsTotal = total;
				return s with
				{
					CurrentOperation = OperationKind.WaitingForSteam,
					Progress = new OperationProgress(OperationKind.WaitingForSteam, null, null, itemsDone, itemsTotal),
					Status = StatusMessage.Of("Status.WaitingForSteam", seconds)
				};
			});
		});
		return await _runner.EnsureSteamRunningAsync(waited, op.Token).ConfigureAwait(continueOnCapturedContext: false);
	}

	private void StartModLogWatch(string gameDir, DateTimeOffset launchedUtc)
	{
		CancellationTokenSource cts = new CancellationTokenSource();
		CancellationTokenSource cancellationTokenSource = Interlocked.Exchange(ref _watchCts, cts);
		try
		{
			cancellationTokenSource?.Cancel();
		}
		catch (ObjectDisposedException)
		{
		}
		BackgroundWatch = Task.Run(() => WatchModLogAsync(gameDir, launchedUtc, cts));
	}

	private void CancelModLogWatch()
	{
		CancellationTokenSource cancellationTokenSource = Interlocked.Exchange(ref _watchCts, null);
		try
		{
			cancellationTokenSource?.Cancel();
		}
		catch (ObjectDisposedException)
		{
		}
	}

	private async Task WatchModLogAsync(string gameDir, DateTimeOffset launchedUtc, CancellationTokenSource cts)
	{
		bool handedOver = false;
		try
		{
			ModLogOutcome outcome = await _runner.WatchModLogAsync(gameDir, launchedUtc, ModLogTimeout, cts.Token).ConfigureAwait(continueOnCapturedContext: false);
			_log.Info($"Mod-log watch: {outcome}");
			bool logExists = File.Exists(ToLocal(gameDir, "MelonLoader/Latest.log"));
			switch (outcome)
			{
			case ModLogOutcome.Loaded:
				PublishWatchStatus(StatusMessage.Of("Status.ModsLoaded"), logExists);
				break;
			case ModLogOutcome.RunningWithoutLog:
				PublishWatchStatus(StatusMessage.Of("Status.ModsNoResponse"), logExists);
				break;
			case ModLogOutcome.GameExitedWithoutLog:
			{
				PublishWatchStatus(null, logExists);
				HelpDialogChoice helpDialogChoice = await _prompts.ShowModsDidNotStartAsync(cts.Token).ConfigureAwait(continueOnCapturedContext: false);
				_log.Info($"Mods-did-not-start dialog: {helpDialogChoice}");
				switch (helpDialogChoice)
				{
				case HelpDialogChoice.OpenLog:
					OpenLauncherLog();
					break;
				case HelpDialogChoice.OpenVcRedist:
					OpenSafely("https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist");
					break;
				case HelpDialogChoice.ReinstallMelonLoader:
					await ReinstallMelonLoaderAsync(cts.Token).ConfigureAwait(continueOnCapturedContext: false);
					break;
				}
				break;
			}
			}
			RaiseWatchEnded(outcome);
			if (outcome != ModLogOutcome.GameExitedWithoutLog)
			{
				ExitWatch = StashWhenGameExitsAsync(gameDir, cts);
				handedOver = true;
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception exception)
		{
			_log.Warn("Mod-log watch failed", exception);
		}
		finally
		{
			if (!handedOver)
			{
				ReleaseWatch(cts);
			}
		}
	}

	private async Task StashWhenGameExitsAsync(string gameDir, CancellationTokenSource cts)
	{
		_ = 1;
		try
		{
			for (int absent = 0; absent < 2; absent = ((!_runner.IsGameRunning()) ? (absent + 1) : 0))
			{
				await Task.Delay(GameExitPoll, _time, cts.Token).ConfigureAwait(continueOnCapturedContext: false);
			}
			if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
			{
				return;
			}
			try
			{
				await StashLoaderCoreAsync(gameDir, "modded game closed", cts.Token).ConfigureAwait(continueOnCapturedContext: false);
			}
			finally
			{
				Volatile.Write(ref _busy, 0);
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception exception)
		{
			_log.Warn("Waiting for the modded game to exit failed", exception);
		}
		finally
		{
			ReleaseWatch(cts);
		}
	}

	private void ReleaseWatch(CancellationTokenSource cts)
	{
		Interlocked.CompareExchange(ref _watchCts, null, cts);
		cts.Dispose();
	}

	private void RaiseWatchEnded(ModLogOutcome outcome)
	{
		try
		{
			ModLogWatchEnded?.Invoke(this, outcome);
		}
		catch (Exception exception)
		{
			_log.Warn("A ModLogWatchEnded handler threw", exception);
		}
	}

	private void PublishWatchStatus(StatusMessage? status, bool logExists)
	{
		bool busy = Volatile.Read(in _busy) != 0;
		Publish((LauncherState s) => s with
		{
			Status = ((busy || (object)status == null) ? s.Status : status),
			CanOpenModLog = ((s.Game?.IsValid ?? false) & logExists)
		});
	}

	private void OpenSafely(string target)
	{
		try
		{
			_runner.OpenInShell(target);
		}
		catch (LauncherException ex)
		{
			LauncherException ex2 = ex;
			LauncherException ex3 = ex2;
			Publish((LauncherState s) => s with
			{
				Banner = new Banner(BannerSeverity.Error, ex3.ToStatusMessage())
			});
		}
		catch (Exception ex4)
		{
			Exception ex5 = ex4;
			Exception ex6 = ex5;
			_log.Warn("Could not open " + target, ex6);
			Publish((LauncherState s) => s with
			{
				Banner = new Banner(BannerSeverity.Error, StatusMessage.Of("Error.Unexpected", ex6.Message))
			});
		}
	}

	private PackManifest? ReadCachedManifest()
	{
		try
		{
			if (!File.Exists(_paths.CachedManifestFile))
			{
				return null;
			}
			return JsonSerializer.Deserialize<PackManifest>(File.ReadAllText(_paths.CachedManifestFile), ManifestJson);
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is NotSupportedException) ? true : false)
		{
			_log.Debug("Cached mod.json unreadable: " + ex.Message);
			return null;
		}
	}

	internal static string ForeignDisplayName(string relative)
	{
		string text = relative.Replace('\\', '/').TrimEnd('/');
		if (!text.StartsWith("UserData/", StringComparison.OrdinalIgnoreCase))
		{
			return Path.GetFileName(text);
		}
		string text2 = text;
		int length = "UserData/".Length;
		return text2.Substring(length, text2.Length - length);
	}

	private static LauncherException ToException(StatusMessage message, string? detail)
	{
		return new LauncherException(message.Key, detail ?? message.Key, null, message.Args.ToArray());
	}

	private static string ToLocal(string gameDir, string relative)
	{
		return Path.Combine(gameDir, relative.Replace('/', Path.DirectorySeparatorChar));
	}

	private static string FullPathOrSelf(string path)
	{
		try
		{
			return Path.GetFullPath(path.Trim());
		}
		catch (Exception ex) when ((ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException) ? true : false)
		{
			return path;
		}
	}

	internal static string SafeFileName(string? assetName)
	{
		string fileName = Path.GetFileName((assetName ?? "").Replace('\\', '/'));
		bool flag = string.IsNullOrWhiteSpace(fileName);
		if (!flag)
		{
			bool flag2 = ((fileName == "." || fileName == "..") ? true : false);
			flag = flag2;
		}
		if (flag || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
		{
			return "pack.bamod";
		}
		return fileName;
	}
}
