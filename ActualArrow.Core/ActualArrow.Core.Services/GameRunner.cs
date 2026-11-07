using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Abstractions;
using ActualArrow.Core.Models;
using ActualArrow.Core.Services.Runner;
using Microsoft.Win32;

namespace ActualArrow.Core.Services;

public sealed class GameRunner : IGameRunner
{
	private readonly ILog _log;

	private readonly TimeProvider _time;

	private readonly IProcessHost _host;

	private readonly RunnerTimings _timings;

	private readonly bool _proton;

	private readonly Func<IReadOnlyList<string>> _steamRoots;

	private readonly Lazy<bool> _underWine;

	private int _startedPid;

	/// <summary>
	/// Vanilla must go through Steam so EAC/Steamworks/online lobby initialize like a normal Steam start.
	/// Direct EACLauncher.exe hangs in lobby on Windows.
	/// </summary>
	public bool VanillaThroughSteam => true;

	public bool IsDryRun { get; }

	public GameRunner(ILog log, bool dryRun, TimeProvider? time = null, Func<IReadOnlyList<string>>? steamRoots = null)
		: this(log, dryRun, time, SystemProcessHost.Instance, RunnerTimings.Default, OperatingSystem.IsLinux(), steamRoots, DetectWine)
	{
	}

	public GameRunner(ILog log, bool dryRun, TimeProvider? time, IProcessHost processHost, RunnerTimings timings, bool proton = false, Func<IReadOnlyList<string>>? steamRoots = null, Func<bool>? underWine = null)
	{
		_log = log;
		IsDryRun = dryRun;
		_time = time ?? TimeProvider.System;
		_host = processHost;
		_timings = timings;
		_proton = proton;
		_steamRoots = steamRoots ?? ((Func<IReadOnlyList<string>>)(() => Array.Empty<string>()));
		_underWine = new Lazy<bool>(underWine ?? ((Func<bool>)(() => false)));
	}

	private static bool DetectWine()
	{
		if (!OperatingSystem.IsWindows())
		{
			return false;
		}
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Wine", writable: false);
			return registryKey != null;
		}
		catch (Exception ex) when ((ex is SecurityException || ex is UnauthorizedAccessException || ex is IOException) ? true : false)
		{
			return false;
		}
	}

	private void RefuseUnderWine()
	{
		if (!_underWine.Value)
		{
			return;
		}
		_log.Warn("Windows launcher under Wine: a Linux Steam is invisible from here, starts are refused");
		throw new LauncherException("Error.UnderWine", "running under Wine", null);
	}

	public bool IsSteamRunning()
	{
		return _host.IsRunning("steam");
	}

	public bool IsGameRunning()
	{
		if (_host.IsRunning("BrokenArrow") || _host.IsRunning("EACLauncher"))
		{
			return true;
		}
		int num = Volatile.Read(in _startedPid);
		if (num == 0)
		{
			return false;
		}
		if (_host.IsAlive(num))
		{
			return true;
		}
		Interlocked.CompareExchange(ref _startedPid, 0, num);
		return false;
	}

	public async Task<bool> EnsureSteamRunningAsync(IProgress<TimeSpan>? waited = null, CancellationToken ct = default(CancellationToken))
	{
		ct.ThrowIfCancellationRequested();
		RefuseUnderWine();
		if (IsDryRun)
		{
			_log.Info("[dry-run] would make sure Steam runs (open steam://open/main if needed)");
			return true;
		}
		if (IsSteamRunning())
		{
			return true;
		}
		_log.Info("Steam is not running: opening steam://open/main");
		try
		{
			_host.ShellOpen("steam://open/main");
		}
		catch (Exception ex) when ((ex is Win32Exception || ex is InvalidOperationException || ex is IOException || ex is PlatformNotSupportedException) ? true : false)
		{
			_log.Warn("Could not open Steam through the shell", ex);
		}
		long started = _time.GetTimestamp();
		while (true)
		{
			TimeSpan elapsedTime = _time.GetElapsedTime(started);
			waited?.Report(elapsedTime);
			if (IsSteamRunning())
			{
				_log.Info($"Steam process appeared after {elapsedTime.TotalSeconds:0.0} s");
				if (_timings.SteamSettleDelay > TimeSpan.Zero)
				{
					await Task.Delay(_timings.SteamSettleDelay, _time, ct).ConfigureAwait(continueOnCapturedContext: false);
				}
				return true;
			}
			if (elapsedTime >= _timings.SteamStartTimeout)
			{
				break;
			}
			TimeSpan timeSpan = _timings.SteamStartTimeout - elapsedTime;
			await Task.Delay((timeSpan < _timings.SteamPollInterval) ? timeSpan : _timings.SteamPollInterval, _time, ct).ConfigureAwait(continueOnCapturedContext: false);
		}
		_log.Warn($"Steam did not start within {_timings.SteamStartTimeout.TotalSeconds:0} s");
		return false;
	}

	public Task<LaunchResult> LaunchModdedAsync(string gameDir, CancellationToken ct = default(CancellationToken))
	{
		ct.ThrowIfCancellationRequested();
		RefuseUnderWine();
		string text = Path.Combine(gameDir, "BrokenArrow.exe");
		if (!File.Exists(text))
		{
			throw new LauncherException("Error.LaunchFailed", text + " not found", null, "BrokenArrow.exe");
		}
		if (_proton)
		{
			ProcessLaunchSpec processLaunchSpec = ProtonLaunch.Build(gameDir, _steamRoots(), _log, out string problem, ModdedArguments(gameDir));
			if ((object)processLaunchSpec == null)
			{
				_log.Error("No Proton launch: " + problem);
				throw new LauncherException("Error.ProtonMissing", problem ?? "no Proton", null);
			}
			return Task.FromResult(Start(LaunchMethod.Proton, processLaunchSpec));
		}
		ProcessLaunchSpec spec = new ProcessLaunchSpec(text, gameDir, Brand.Game.LaunchEnvironment, ModdedArguments(gameDir));
		return Task.FromResult(Start(LaunchMethod.DirectExe, spec));
	}

	private IReadOnlyList<string> ModdedArguments(string gameDir)
	{
		if (!Brand.Game.IsFirstModdedStart(gameDir))
		{
			return Brand.Game.ModdedArguments;
		}
		_log.Info("First modded start in this folder: MelonLoader console left visible for the assembly generation");
		return Array.Empty<string>();
	}

	public Task<LaunchResult> LaunchVanillaAsync(string gameDir, CancellationToken ct = default(CancellationToken))
	{
		ct.ThrowIfCancellationRequested();
		List<string> list = FindProxiesInRoot(gameDir).Concat(LoaderScan.FindOtherLoaders(gameDir, _log)).ToList();
		if (list.Count > 0)
		{
			_log.Error("Refusing a vanilla start: proxy in the game root (" + string.Join(", ", list) + ")");
			throw new InvalidOperationException("A mod-loader proxy is still in the game root: " + string.Join(", ", list) + ". Stash it first.");
		}
		RefuseUnderWine();

		// Always use Steam protocol for vanilla. Direct EACLauncher.exe / BrokenArrow.exe
		// skips Steam's launch pipeline and can hang while loading into the online lobby.
		string uri = Brand.Steam.RunGameUri;
		_log.Info("Vanilla start through Steam: " + uri);
		DateTimeOffset utcNow = _time.GetUtcNow();
		if (IsDryRun)
		{
			_log.Info("[dry-run] would open " + uri);
			return Task.FromResult(new LaunchResult(LaunchMethod.SteamUri, DryRun: true, null, utcNow, uri));
		}
		try
		{
			_host.ShellOpen(uri);
		}
		catch (Exception ex) when ((ex is Win32Exception || ex is InvalidOperationException || ex is IOException || ex is PlatformNotSupportedException) ? true : false)
		{
			_log.Error("Could not open " + uri, ex);
			throw new LauncherException("Error.LaunchFailed", ex.Message, ex);
		}
		_log.Info("Opened " + uri);
		return Task.FromResult(new LaunchResult(LaunchMethod.SteamUri, DryRun: false, null, utcNow, uri));
	}

	public async Task<ModLogOutcome> WatchModLogAsync(string gameDir, DateTimeOffset launchedUtc, TimeSpan timeout, CancellationToken ct = default(CancellationToken))
	{
		string logPath = Path.Combine(gameDir, "MelonLoader/Latest.log".Replace('/', Path.DirectorySeparatorChar));
		DateTime threshold = launchedUtc.UtcDateTime - _timings.LogTimeTolerance;
		long started = _time.GetTimestamp();
		int absentInARow = 0;
		bool flag;
		while (true)
		{
			ct.ThrowIfCancellationRequested();
			if (LogWrittenSince(logPath, threshold))
			{
				_log.Info("Mod log written after launch: mods loaded");
				return ModLogOutcome.Loaded;
			}
			TimeSpan elapsedTime = _time.GetElapsedTime(started);
			flag = IsGameRunning();
			absentInARow = ((!flag) ? (absentInARow + 1) : 0);
			if (absentInARow >= 2 && _time.GetUtcNow() - launchedUtc >= _timings.GameExitGrace)
			{
				if (LogWrittenSince(logPath, threshold))
				{
					return ModLogOutcome.Loaded;
				}
				_log.Warn("Game exited without writing the mod log");
				return ModLogOutcome.GameExitedWithoutLog;
			}
			if (elapsedTime >= timeout)
			{
				break;
			}
			TimeSpan timeSpan = timeout - elapsedTime;
			await Task.Delay((timeSpan < _timings.ModLogPollInterval) ? timeSpan : _timings.ModLogPollInterval, _time, ct).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (LogWrittenSince(logPath, threshold))
		{
			return ModLogOutcome.Loaded;
		}
		if (flag)
		{
			_log.Warn($"No mod log after {timeout.TotalSeconds:0} s, game still running");
			return ModLogOutcome.RunningWithoutLog;
		}
		_log.Warn($"No mod log after {timeout.TotalSeconds:0} s and the game is not running");
		return ModLogOutcome.GameExitedWithoutLog;
	}

	public void OpenInShell(string pathOrUri)
	{
		if (IsDryRun)
		{
			_log.Info("[dry-run] would open in the shell: " + pathOrUri);
			return;
		}
		try
		{
			_host.ShellOpen(pathOrUri);
			_log.Debug("Opened in the shell: " + pathOrUri);
		}
		catch (Exception ex) when ((ex is Win32Exception || ex is InvalidOperationException || ex is IOException || ex is PlatformNotSupportedException) ? true : false)
		{
			_log.Warn("Shell open failed: " + pathOrUri, ex);
			throw new LauncherException("Error.Unexpected", ex.Message, ex, ex.Message);
		}
	}

	private LaunchResult Start(LaunchMethod method, ProcessLaunchSpec spec)
	{
		string text = spec.Describe();
		DateTimeOffset utcNow = _time.GetUtcNow();
		if (IsDryRun)
		{
			_log.Info($"[dry-run] would start ({method}): {text}");
			return new LaunchResult(method, DryRun: true, null, utcNow, text);
		}
		try
		{
			int? num = _host.Start(spec);
			if (num.HasValue)
			{
				int valueOrDefault = num.GetValueOrDefault();
				Volatile.Write(ref _startedPid, valueOrDefault);
			}
			_log.Info($"Started ({method}) pid={num?.ToString() ?? "?"}: {text}");
			return new LaunchResult(method, DryRun: false, num, utcNow, text);
		}
		catch (Exception ex) when ((ex is Win32Exception || ex is InvalidOperationException || ex is IOException || ex is PlatformNotSupportedException || ex is UnauthorizedAccessException) ? true : false)
		{
			_log.Error($"Could not start ({method}): {text}", ex);
			throw new LauncherException("Error.LaunchFailed", ex.Message, ex);
		}
	}

	internal static IReadOnlyList<string> FindProxiesInRoot(string gameDir)
	{
		if (!Directory.Exists(gameDir))
		{
			return Array.Empty<string>();
		}
		List<string> list = new List<string>();
		foreach (string item in Directory.EnumerateFiles(gameDir))
		{
			string name = Path.GetFileName(item);
			if (Brand.Game.ProxyDllNames.Any((string p) => string.Equals(p, name, StringComparison.OrdinalIgnoreCase)))
			{
				list.Add(name);
			}
		}
		return list;
	}

	private static bool LogWrittenSince(string path, DateTime thresholdUtc)
	{
		try
		{
			return File.Exists(path) && File.GetLastWriteTimeUtc(path) >= thresholdUtc;
		}
		catch (IOException)
		{
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}
	}
}
