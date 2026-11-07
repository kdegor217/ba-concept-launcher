using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Abstractions;
using ActualArrow.Core.Models;
using ActualArrow.Core.Services.Install;

namespace ActualArrow.Core.Services;

public sealed class MelonLoaderInstaller : IMelonLoaderInstaller
{
	private const string FolderItem = "MelonLoader/";

	private readonly IAppPaths _paths;

	private readonly IInstallRecordStore _records;

	private readonly IDownloader _downloader;

	private readonly IProxyManager _proxies;

	private readonly ILog _log;

	private readonly TimeProvider _time;

	private readonly MelonLoaderSource _source;

	private readonly Func<bool> _isGameRunning;

	internal Action<string>? FaultHook { get; set; }

	public MelonLoaderInstaller(IAppPaths paths, IInstallRecordStore records, IDownloader downloader, IProxyManager proxies, ILog log, TimeProvider? time = null, MelonLoaderSource? source = null, Func<bool>? isGameRunning = null)
	{
		_paths = paths;
		_records = records;
		_downloader = downloader;
		_proxies = proxies;
		_log = log;
		_time = time ?? TimeProvider.System;
		_source = source ?? MelonLoaderSource.Pinned;
		_isGameRunning = isGameRunning ?? new Func<bool>(GameGuard.IsGameProcessRunning);
	}

	public async Task<MelonLoaderStatus> GetStatusAsync(string gameDir, CancellationToken ct = default(CancellationToken))
	{
		InstallRecord installRecord = await _records.ReadAsync(gameDir, ct).ConfigureAwait(continueOnCapturedContext: false);
		bool flag = PackPaths.FindChild(gameDir, "MelonLoader", directory: true) != null;
		ProxyState state = _proxies.GetState(gameDir);
		bool flag2 = state.InRoot.Contains("version.dll", StringComparer.OrdinalIgnoreCase);
		bool flag3 = state.Stashed.Contains("version.dll", StringComparer.OrdinalIgnoreCase);
		string text = installRecord?.Loader?.Tag;
		MelonLoaderState state2 = (((object)installRecord?.Loader == null) ? ((!flag) ? MelonLoaderState.NotInstalled : MelonLoaderState.UnrecordedFolder) : ((!((text == "v0.7.3") & flag) || !PackInstaller.RecordsMelonFolder(installRecord) || !(flag2 | flag3)) ? MelonLoaderState.NeedsReinstall : MelonLoaderState.Ready));
		return new MelonLoaderStatus(state2, text, flag, flag2, flag3);
	}

	public async Task InstallAsync(string gameDir, bool replaceUnrecordedFolder, IProgress<OperationProgress>? progress = null, CancellationToken ct = default(CancellationToken))
	{
		ct.ThrowIfCancellationRequested();
		GameGuard.EnsureNotRunning(_isGameRunning);
		string game = GameGuard.RequireGameDir(gameDir);
		bool flag = PackInstaller.RecordsMelonFolder(await _records.ReadAsync(game, ct).ConfigureAwait(continueOnCapturedContext: false));
		string text = PackPaths.FindChild(game, "MelonLoader", directory: true);
		if (text != null && !flag && !replaceUnrecordedFolder)
		{
			throw new InstallException(InstallFailure.UnrecordedMelonLoader, "A MelonLoader/ folder exists that this launcher did not install: " + text);
		}
		string archivePath = await ObtainArchiveAsync(progress, ct).ConfigureAwait(continueOnCapturedContext: false);
		await Task.Run(() => ExtractAndCommitAsync(game, archivePath, replaceUnrecordedFolder, progress, ct), ct).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task<string> ObtainArchiveAsync(IProgress<OperationProgress>? progress, CancellationToken ct)
	{
		Directory.CreateDirectory(_paths.DownloadsDir);
		string dest = Path.Combine(_paths.DownloadsDir, Path.GetFileName(_source.FileName));
		if (await Task.Run(() => IsVerified(dest, ct), ct).ConfigureAwait(continueOnCapturedContext: false))
		{
			_log.Info("Reusing verified " + Path.GetFileName(dest));
			return dest;
		}
		_log.Info($"Downloading MelonLoader {"v0.7.3"} from {_source.Url}");
		InlineProgress<DownloadProgress> progress2 = ((progress == null) ? null : new InlineProgress<DownloadProgress>((DownloadProgress p) =>
		{
			progress.Report(new OperationProgress(OperationKind.DownloadingMelonLoader, p.BytesDone, p.BytesTotal ?? _source.Size));
		}));
		progress?.Report(new OperationProgress(OperationKind.DownloadingMelonLoader, 0L, _source.Size));
		await _downloader.DownloadAsync(new DownloadRequest(_source.Url, dest, _source.Size, _source.Sha256), progress2, ct).ConfigureAwait(continueOnCapturedContext: false);
		if (!(await Task.Run(() => IsVerified(dest, ct), ct).ConfigureAwait(continueOnCapturedContext: false)))
		{
			FsOps.TryDeleteFile(dest);
			throw new InstallException(InstallFailure.InvalidMelonLoader, "Downloaded " + Path.GetFileName(dest) + " does not match the pinned size/SHA-256.");
		}
		return dest;
	}

	private bool IsVerified(string path, CancellationToken ct)
	{
		try
		{
			FileInfo fileInfo = new FileInfo(path);
			return fileInfo.Exists && fileInfo.Length == _source.Size && Hashing.SameHash(Hashing.FileSha256(path, ct), _source.Sha256);
		}
		catch (IOException)
		{
			return false;
		}
	}

	private async Task ExtractAndCommitAsync(string game, string archivePath, bool replaceUnrecordedFolder, IProgress<OperationProgress>? progress, CancellationToken ct)
	{
		string launcherDir = PackPaths.ResolveInside(game, "UserData/ActualArrow");
		string staging = Path.Combine(launcherDir, "ml-staging");
		BackupFolders backups = new BackupFolders(_paths.BackupDir, _time, _log);
		FileTransaction tx = new FileTransaction(backups, launcherDir, _log, FaultHook);
		if (Directory.Exists(launcherDir))
		{
			foreach (string item in Directory.EnumerateDirectories(launcherDir, "trash-*"))
			{
				if (FsOps.TryDeleteDirectory(item))
				{
					_log.Info("Leftover trash removed: " + Path.GetFileName(item));
				}
			}
		}
		try
		{
			FsOps.DeleteDirectory(staging);
			Directory.CreateDirectory(staging);
			StageArchive(archivePath, staging, progress, ct);
			InstallRecord installRecord = await _records.ReadAsync(game, ct).ConfigureAwait(continueOnCapturedContext: false);
			IReadOnlyList<string> source = installRecord?.Loader?.Items ?? Array.Empty<string>();
			bool flag = PackInstaller.RecordsMelonFolder(installRecord);
			bool flag2 = source.Contains("version.dll", StringComparer.OrdinalIgnoreCase);
			string text = PackPaths.FindChild(game, "MelonLoader", directory: true);
			if (text != null)
			{
				ct.ThrowIfCancellationRequested();
				if (flag)
				{
					tx.Trash(text, isDirectory: true);
				}
				else
				{
					if (!replaceUnrecordedFolder)
					{
						throw new InstallException(InstallFailure.UnrecordedMelonLoader, "Unrecorded folder appeared: " + text);
					}
					tx.Backup(text, "MelonLoader", isDirectory: true);
				}
			}
			string text2 = PackPaths.FindChild(game, "version.dll", directory: false);
			if (text2 != null)
			{
				if (flag2)
				{
					tx.Trash(text2, isDirectory: false);
				}
				else
				{
					tx.Backup(text2, "version.dll", isDirectory: false);
				}
			}
			string text3 = PackPaths.FindChild(_paths.StashDir, "version.dll", directory: false);
			if (text3 != null)
			{
				tx.Trash(text3, isDirectory: false);
			}
			if (PackPaths.FindChild(game, "winhttp.dll", directory: false) != null)
			{
				_log.Warn("winhttp.dll (another mod loader's proxy) is in the game root; it is left alone.");
			}
			ct.ThrowIfCancellationRequested();
			tx.Place(Path.Combine(staging, "MelonLoader"), Path.Combine(game, "MelonLoader"), isDirectory: true);
			tx.Place(Path.Combine(staging, "version.dll"), Path.Combine(game, "version.dll"), isDirectory: false);
			InstallRecord installRecord2 = installRecord ?? new InstallRecord();
			InstallRecord record = installRecord2 with
			{
				Schema = 1,
				Loader = new LoaderInstall
				{
					Tag = "v0.7.3",
					InstalledUtc = _time.GetUtcNow(),
					Items = new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "MelonLoader/", "version.dll" })
				}
			};
			ct.ThrowIfCancellationRequested();
			tx.Checkpoint("record");
			await _records.WriteAsync(game, record, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex)
		{
			bool flag3 = tx.Rollback();
			FsOps.TryDeleteDirectory(staging);
			FsOps.PruneEmptyUpwards(launcherDir, game);
			if (!flag3)
			{
				throw new InstallException(InstallFailure.RollbackFailed, "MelonLoader install failed and the rollback was incomplete: " + ex.Message, ex);
			}
			if (!(ex is OperationCanceledException) && !(ex is LauncherException))
			{
				if (ex is InvalidDataException)
				{
					throw new InstallException(InstallFailure.InvalidMelonLoader, "MelonLoader archive is broken: " + ex.Message, ex);
				}
				_log.Error("MelonLoader install failed and was rolled back", ex);
				throw new InstallException(InstallFailure.RolledBack, "MelonLoader install failed and was rolled back: " + ex.Message, ex);
			}
			throw;
		}
		tx.Complete();
		FsOps.TryDeleteDirectory(staging);
		backups.Prune();
		_log.Info("MelonLoader v0.7.3 installed into " + game + ((tx.BackupDir == null) ? "." : (", backup " + tx.BackupDir + ".")));
	}

	private void StageArchive(string archivePath, string staging, IProgress<OperationProgress>? progress, CancellationToken ct)
	{
		ZipArchive zipArchive;
		try
		{
			zipArchive = ZipFile.OpenRead(archivePath);
		}
		catch (Exception ex) when ((ex is InvalidDataException || ex is IOException) ? true : false)
		{
			throw new InstallException(InstallFailure.InvalidMelonLoader, "Cannot open " + archivePath + ": " + ex.Message, ex);
		}
		using (zipArchive)
		{
			List<(string, ZipArchiveEntry)> list = new List<(string, ZipArchiveEntry)>();
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (ZipArchiveEntry entry in zipArchive.Entries)
			{
				if (!PackPaths.TrySplit(entry.FullName, out string[] segments, out bool isDirectory, out string reason))
				{
					_log.Warn($"MelonLoader entry skipped: '{entry.FullName}' ({reason})");
				}
				else
				{
					if (isDirectory)
					{
						continue;
					}
					string text;
					if (segments.Length == 1 && segments[0].Equals("version.dll", StringComparison.OrdinalIgnoreCase))
					{
						text = "version.dll";
					}
					else
					{
						if (segments.Length < 2 || !segments[0].Equals("MelonLoader", StringComparison.OrdinalIgnoreCase))
						{
							_log.Info("MelonLoader entry skipped: '" + entry.FullName + "' (outside version.dll and MelonLoader/)");
							continue;
						}
						text = "MelonLoader/" + string.Join('/', segments.Skip(1));
					}
					if (hashSet.Add(text))
					{
						list.Add((text, entry));
					}
				}
			}
			bool flag = list.Any(((string Rel, ZipArchiveEntry Entry) w) => w.Rel == "version.dll");
			int num = Enumerable.Count(list, ((string Rel, ZipArchiveEntry Entry) w) => w.Rel != "version.dll");
			if (!flag || num == 0)
			{
				throw new InstallException(InstallFailure.InvalidMelonLoader, "MelonLoader archive lacks " + (flag ? "MelonLoader/" : "version.dll") + ".");
			}
			int num2 = 0;
			foreach (var (relative, zipArchiveEntry) in list)
			{
				ct.ThrowIfCancellationRequested();
				string text2 = PackPaths.ResolveInside(staging, relative);
				Directory.CreateDirectory(Path.GetDirectoryName(text2));
				using Stream source = zipArchiveEntry.Open();
				Hashing.CopyToNewFile(source, text2, zipArchiveEntry.Length, ct);
				if (progress != null)
				{
					int? itemsDone = (num2 += 1);
					int? itemsTotal = list.Count;
					progress.Report(new OperationProgress(OperationKind.InstallingMelonLoader, null, null, itemsDone, itemsTotal));
				}
			}
		}
	}
}
