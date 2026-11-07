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

public sealed class PackInstaller : IPackInstaller
{
	private sealed record StagedFile(string Rel, string StagedPath, string Target, long Size, string Sha256);

	private readonly IAppPaths _paths;

	private readonly IInstallRecordStore _records;

	private readonly IProxyManager _proxies;

	private readonly ILog _log;

	private readonly TimeProvider _time;

	private readonly Func<bool> _isGameRunning;

	internal Action<string>? FaultHook { get; set; }

	private BackupFolders Backups => new BackupFolders(_paths.BackupDir, _time, _log);

	public PackInstaller(IAppPaths paths, IInstallRecordStore records, IProxyManager proxies, ILog log, TimeProvider? time = null, Func<bool>? isGameRunning = null)
	{
		_paths = paths;
		_records = records;
		_proxies = proxies;
		_log = log;
		_time = time ?? TimeProvider.System;
		_isGameRunning = isGameRunning ?? new Func<bool>(GameGuard.IsGameProcessRunning);
	}

	public Task<PackInspection> InspectAsync(string packPath, CancellationToken ct = default(CancellationToken))
	{
		return Task.Run(() =>
		{
			using PackArchive packArchive = PackArchive.Open(packPath, _log);
			return packArchive.Inspection;
		}, ct);
	}

	public async Task<PackHealth> CheckQuickAsync(string gameDir, CancellationToken ct = default(CancellationToken))
	{
		InstallRecord record = await _records.ReadAsync(gameDir, ct).ConfigureAwait(continueOnCapturedContext: false);
		if ((object)record?.Pack == null)
		{
			return new PackHealth(record, Array.Empty<string>());
		}
		List<string> missingOrResized = await Task.Run(() =>
		{
			List<string> list = new List<string>();
			foreach (InstalledFile file in record.Pack.Files)
			{
				ct.ThrowIfCancellationRequested();
				if (!TryRecordedFile(gameDir, file.Path, out string fullPath))
				{
					list.Add(file.Path);
				}
				else
				{
					FileInfo fileInfo = new FileInfo(fullPath);
					if (!fileInfo.Exists || fileInfo.Length != file.Size)
					{
						list.Add(file.Path);
					}
				}
			}
			return list;
		}, ct).ConfigureAwait(continueOnCapturedContext: false);
		return new PackHealth(record, missingOrResized);
	}

	public async Task<PackVerifyResult> VerifyAsync(string gameDir, IProgress<OperationProgress>? progress = null, CancellationToken ct = default(CancellationToken))
	{
		InstallRecord installRecord = await _records.ReadAsync(gameDir, ct).ConfigureAwait(continueOnCapturedContext: false);
		if ((object)installRecord?.Pack == null)
		{
			throw new InstallException(InstallFailure.NotInstalled, "Verify: no pack is recorded in install.json.");
		}
		IReadOnlyList<InstalledFile> files = installRecord.Pack.Files;
		return await Task.Run(() =>
		{
			List<string> list = new List<string>();
			List<string> list2 = new List<string>();
			int num = 0;
			IProgress<OperationProgress>? progress2 = progress;
			if (progress2 != null)
			{
				int? itemsDone = 0;
				int? itemsTotal = files.Count;
				progress2.Report(new OperationProgress(OperationKind.Verifying, null, null, itemsDone, itemsTotal));
			}
			foreach (InstalledFile item in files)
			{
				ct.ThrowIfCancellationRequested();
				if (!TryRecordedFile(gameDir, item.Path, out string fullPath) || !File.Exists(fullPath))
				{
					list.Add(item.Path);
				}
				else if (new FileInfo(fullPath).Length != item.Size || !Hashing.SameHash(Hashing.FileSha256(fullPath, ct), item.Sha256))
				{
					list2.Add(item.Path);
				}
				IProgress<OperationProgress>? progress3 = progress;
				if (progress3 != null)
				{
					int? itemsTotal = (num += 1);
					int? itemsDone = files.Count;
					progress3.Report(new OperationProgress(OperationKind.Verifying, null, null, itemsTotal, itemsDone));
				}
			}
			_log.Info($"Verify: {files.Count} checked, {list.Count} missing, {list2.Count} changed.");
			return new PackVerifyResult(files.Count, list, list2);
		}, ct).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<IReadOnlyList<string>> FindForeignModsAsync(string gameDir, PackInspection pack, CancellationToken ct = default(CancellationToken))
	{
		if ((object)(await _records.ReadAsync(gameDir, ct).ConfigureAwait(continueOnCapturedContext: false))?.Pack != null)
		{
			return Array.Empty<string>();
		}
		HashSet<string> inPack = pack.Entries.Select((PackEntry e) => e.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
		List<string> list = await Task.Run(() =>
		{
			List<string> list2 = new List<string>();
			string[] array = new string[2] { "Mods", "Plugins" };
			foreach (string text in array)
			{
				string text2 = PackPaths.FindChild(gameDir, text, directory: true);
				if (text2 != null)
				{
					foreach (string item2 in Directory.EnumerateFiles(text2))
					{
						string fileName = Path.GetFileName(item2);
						if (fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
						{
							string item = text + "/" + fileName;
							if (!inPack.Contains(item))
							{
								list2.Add(item);
							}
						}
					}
				}
			}
			foreach (string item3 in PackUserDataFolders(pack))
			{
				ct.ThrowIfCancellationRequested();
				string text3 = FindRelative(gameDir, item3);
				if (text3 != null)
				{
					foreach (string item4 in Directory.EnumerateFiles(text3, "*", SearchOption.AllDirectories))
					{
						string text4 = item3 + "/" + Path.GetRelativePath(text3, item4).Replace('\\', '/');
						if (!inPack.Contains(text4) && PackPaths.IsValidRecordedPackFile(text4))
						{
							list2.Add(text4);
						}
					}
				}
			}
			return list2;
		}, ct).ConfigureAwait(continueOnCapturedContext: false);
		list.Sort(StringComparer.OrdinalIgnoreCase);
		if (list.Count > 0)
		{
			_log.Info("Files not in the pack: " + string.Join(", ", list));
		}
		return list;
	}

	private static IEnumerable<string> PackUserDataFolders(PackInspection pack)
	{
		return (from f in pack.Entries.Select((PackEntry e) => UserDataFolderOf(e.RelativePath)).OfType<string>()
			where !f.Equals("UserData/ActualArrow", StringComparison.OrdinalIgnoreCase)
			select f).Distinct(StringComparer.OrdinalIgnoreCase);
	}

	private static string? FindRelative(string baseDir, string relative)
	{
		string[] array = relative.Split('/');
		string text = baseDir;
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = PackPaths.FindChild(text, array[i], directory: true);
			if (text2 == null && i == array.Length - 1)
			{
				text2 = PackPaths.FindChild(text, array[i], directory: false);
			}
			if (text2 == null)
			{
				return null;
			}
			text = text2;
		}
		return text;
	}

	public Task<PackInstallResult> InstallAsync(PackInstallRequest request, IProgress<OperationProgress>? progress = null, CancellationToken ct = default(CancellationToken))
	{
		return Task.Run(() => InstallCoreAsync(request, progress, ct), ct);
	}

	private async Task<PackInstallResult> InstallCoreAsync(PackInstallRequest req, IProgress<OperationProgress>? progress, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		GameGuard.EnsureNotRunning(_isGameRunning);
		string gameDir = GameGuard.RequireGameDir(req.GameDir);
		_log.Info($"Installing pack {req.Repo} {req.Tag} ({req.AssetName}) into {gameDir}");
		InstallRecord installRecord = await _records.ReadAsync(gameDir, ct).ConfigureAwait(continueOnCapturedContext: false);
		PackRecord packRecord = installRecord?.Pack;
		int total;
		int done;
		using (PackArchive archive = PackArchive.Open(req.PackPath, _log))
		{
			IReadOnlyList<(PackEntry, ZipArchiveEntry)> files = archive.Files;
			if (files.Count == 0)
			{
				throw new InstallException(InstallFailure.InvalidPack, "Pack '" + req.PackPath + "' has no installable files.");
			}
			string trashParent = PackPaths.ResolveInside(gameDir, "UserData/ActualArrow");
			string staging = PackPaths.ResolveInside(gameDir, "UserData/ActualArrow/staging");
			FileTransaction tx = new FileTransaction(Backups, trashParent, _log, FaultHook);
			total = files.Count * 2;
			done = 0;
			int added = 0;
			int changed = 0;
			int unchanged = 0;
			int removed = 0;
			List<InstalledFile> list = new List<InstalledFile>();
			List<string> removedPaths = new List<string>();
			int foreignMovedTotal;
			InstallRecord newRecord;
			try
			{
				IProgress<OperationProgress>? progress2 = progress;
				if (progress2 != null)
				{
					int? itemsDone = 0;
					int? itemsTotal = total;
					progress2.Report(new OperationProgress(OperationKind.InstallingPack, null, null, itemsDone, itemsTotal));
				}
				FsOps.DeleteDirectory(staging);
				Directory.CreateDirectory(staging);
				List<StagedFile> list2 = new List<StagedFile>(files.Count);
				foreach (var (packEntry, zipArchiveEntry) in files)
				{
					ct.ThrowIfCancellationRequested();
					string target = PackPaths.ResolveInside(gameDir, packEntry.RelativePath);
					string text = PackPaths.ResolveInside(staging, packEntry.RelativePath);
					Directory.CreateDirectory(Path.GetDirectoryName(text));
					tx.Checkpoint("stage:" + packEntry.RelativePath);
					using (Stream source = zipArchiveEntry.Open())
					{
						var (size, sha) = Hashing.CopyToNewFile(source, text, zipArchiveEntry.Length, ct);
						list2.Add(new StagedFile(packEntry.RelativePath, text, target, size, sha));
					}
					Step();
				}
				Dictionary<string, InstalledFile> dictionary = new Dictionary<string, InstalledFile>(StringComparer.Ordinal);
				foreach (InstalledFile item in packRecord?.Files ?? Array.Empty<InstalledFile>())
				{
					dictionary.TryAdd(item.Path, item);
				}
				HashSet<string> newPaths = list2.Select((StagedFile s) => s.Rel).ToHashSet(StringComparer.Ordinal);
				HashSet<string> hashSet = list2.Select((StagedFile s) => s.Rel).ToHashSet(StringComparer.OrdinalIgnoreCase);
				List<string> second = (from f in list2.Select((StagedFile s) => UserDataFolderOf(s.Rel)).OfType<string>().Distinct(StringComparer.Ordinal)
					where !Directory.Exists(PackPaths.ResolveInside(gameDir, f))
					select f).ToList();
				foreach (InstalledFile item2 in dictionary.Values.Where((InstalledFile f) => !newPaths.Contains(f.Path)))
				{
					ct.ThrowIfCancellationRequested();
					removed++;
					if (!TryRecordedFile(gameDir, item2.Path, out string fullPath))
					{
						_log.Warn("Recorded path ignored (not a pack path): " + item2.Path);
					}
					else if (File.Exists(fullPath))
					{
						tx.Backup(fullPath, item2.Path, isDirectory: false);
						removedPaths.Add(fullPath);
					}
				}
				int num = 0;
				if (req.ForeignModsAction == ForeignModsAction.MoveToBackup)
				{
					HashSet<string> hashSet2 = PackUserDataFolders(archive.Inspection).ToHashSet(StringComparer.OrdinalIgnoreCase);
					foreach (string item3 in req.ForeignMods.Distinct(StringComparer.OrdinalIgnoreCase))
					{
						ct.ThrowIfCancellationRequested();
						EntryVerdict entryVerdict = PackPaths.ClassifyPackEntry(item3);
						bool flag = entryVerdict.Kind == EntryKind.PackFile && !hashSet.Contains(entryVerdict.RelativePath);
						if (flag)
						{
							string text2 = entryVerdict.RelativePath.Split('/')[0];
							int num2;
							if ((!(text2 == "Mods") && !(text2 == "Plugins")) || 1 == 0)
							{
								string text3 = UserDataFolderOf(entryVerdict.RelativePath);
								num2 = ((text3 != null && hashSet2.Contains(text3)) ? 1 : 0);
							}
							else
							{
								num2 = 1;
							}
							flag = (byte)num2 != 0;
						}
						if (!flag)
						{
							_log.Warn("Foreign entry ignored: " + item3);
							continue;
						}
						string text4 = FindRelative(gameDir, entryVerdict.RelativePath);
						if (text4 != null && File.Exists(text4))
						{
							tx.Backup(text4, entryVerdict.RelativePath, isDirectory: false);
							removedPaths.Add(text4);
							num++;
							_log.Info("Moved to backup (not in the pack): " + entryVerdict.RelativePath);
						}
					}
				}
				foreignMovedTotal = num;
				foreach (StagedFile item4 in list2)
				{
					ct.ThrowIfCancellationRequested();
					list.Add(new InstalledFile(item4.Rel, item4.Size, item4.Sha256));
					bool flag2 = File.Exists(item4.Target);
					if ((!req.ForceRewrite & flag2) && dictionary.TryGetValue(item4.Rel, out var value) && value.Size == item4.Size && Hashing.SameHash(value.Sha256, item4.Sha256) && new FileInfo(item4.Target).Length == item4.Size)
					{
						unchanged++;
						Step();
						continue;
					}
					if (flag2)
					{
						tx.Backup(item4.Target, item4.Rel, isDirectory: false);
						changed++;
					}
					else
					{
						added++;
					}
					tx.Place(item4.StagedPath, item4.Target, isDirectory: false);
					Step();
				}
				List<string> createdUserDataFolders = (packRecord?.CreatedUserDataFolders ?? Array.Empty<string>()).Concat(second).Where(PackPaths.IsValidUserDataFolder).Distinct(StringComparer.Ordinal)
					.Order(StringComparer.Ordinal)
					.ToList();
				InstallRecord installRecord2 = installRecord ?? new InstallRecord();
				newRecord = installRecord2 with
				{
					Schema = 1,
					Pack = new PackRecord
					{
						Repo = req.Repo,
						Tag = req.Tag,
						Manifest = archive.Inspection.Manifest,
						AssetName = req.AssetName,
						AssetSha256 = req.AssetSha256.ToLowerInvariant(),
						InstalledUtc = _time.GetUtcNow(),
						Files = list.OrderBy((InstalledFile f) => f.Path, StringComparer.Ordinal).ToList(),
						CreatedUserDataFolders = createdUserDataFolders
					}
				};
				ct.ThrowIfCancellationRequested();
				tx.Checkpoint("record");
				await _records.WriteAsync(gameDir, newRecord, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (Exception ex)
			{
				throw RollbackAndWrap(tx, staging, ex, "install");
			}
			tx.Complete();
			FsOps.TryDeleteDirectory(staging);
			foreach (string item5 in removedPaths)
			{
				PruneEmptyParents(gameDir, item5);
			}
			CacheManifest(archive);
			if (foreignMovedTotal > 0 && tx.BackupDir != null)
			{
				Backups.MarkKept(tx.BackupDir, "other mods and files that were not in the pack, moved here on the first install");
			}
			Backups.Prune();
			_log.Info($"Pack {req.Tag} installed: {added} added, {changed} changed, {removed} removed, {unchanged} unchanged" + ((tx.BackupDir == null) ? "." : (", backup " + tx.BackupDir + ".")));
			return new PackInstallResult(newRecord, added, changed, removed, unchanged, tx.BackupDir, archive.Inspection.Skipped);
		}
		void Step()
		{
			IProgress<OperationProgress>? progress3 = progress;
			if (progress3 != null)
			{
				int? itemsDone2 = (done += 1);
				progress3.Report(new OperationProgress(ItemsTotal: total, Kind: OperationKind.InstallingPack, BytesDone: null, BytesTotal: null, ItemsDone: itemsDone2));
			}
		}
	}

	public async Task<UninstallPlan> GetUninstallPlanAsync(string gameDir, CancellationToken ct = default(CancellationToken))
	{
		InstallRecord installRecord = await _records.ReadAsync(gameDir, ct).ConfigureAwait(continueOnCapturedContext: false);
		ProxyState state = _proxies.GetState(gameDir);
		List<string> proxies = state.InRoot.Concat(state.Stashed.Select((string p) => p + " (stashed)")).ToList();
		List<string> userDataFolders = (installRecord?.Pack?.CreatedUserDataFolders ?? Array.Empty<string>()).Where((string f) => PackPaths.IsValidUserDataFolder(f) && Directory.Exists(PackPaths.ResolveInside(gameDir, f))).ToList();
		bool hasMelonLoader = RecordsMelonFolder(installRecord) && PackPaths.FindChild(gameDir, "MelonLoader", directory: true) != null;
		return new UninstallPlan(gameDir, (installRecord?.Pack?.Files.Count).GetValueOrDefault(), hasMelonLoader, proxies, userDataFolders);
	}

	public Task<UninstallResult> UninstallAsync(string gameDir, UninstallOptions options, IProgress<OperationProgress>? progress = null, CancellationToken ct = default(CancellationToken))
	{
		return Task.Run(() => UninstallCoreAsync(gameDir, options, progress, ct), ct);
	}

	private async Task<UninstallResult> UninstallCoreAsync(string gameDirIn, UninstallOptions options, IProgress<OperationProgress>? progress, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		GameGuard.EnsureNotRunning(_isGameRunning);
		string gameDir = GameGuard.RequireGameDir(gameDirIn);
		InstallRecord installRecord = await _records.ReadAsync(gameDir, ct).ConfigureAwait(continueOnCapturedContext: false);
		_log.Info($"Uninstalling from {gameDir} (remove user data: {options.RemoveUserData})");
		FileTransaction fileTransaction = new FileTransaction(Backups, null, _log, FaultHook);
		IReadOnlyList<InstalledFile> readOnlyList = installRecord?.Pack?.Files ?? Array.Empty<InstalledFile>();
		int total = readOnlyList.Count + 4;
		int done = 0;
		List<string> list = new List<string>();
		List<string> userDataFolders = new List<string>();
		try
		{
			IProgress<OperationProgress>? progress2 = progress;
			if (progress2 != null)
			{
				int? itemsDone = 0;
				int? itemsTotal = total;
				progress2.Report(new OperationProgress(OperationKind.Uninstalling, null, null, itemsDone, itemsTotal));
			}
			userDataFolders = (options.RemoveUserData ? (installRecord?.Pack?.CreatedUserDataFolders ?? Array.Empty<string>()).Where((string f) =>
			{
				if (PackPaths.IsValidUserDataFolder(f))
				{
					return true;
				}
				_log.Warn("Recorded UserData folder ignored: " + f);
				return false;
			}).Distinct(StringComparer.OrdinalIgnoreCase).ToList() : new List<string>());
			foreach (InstalledFile item in readOnlyList)
			{
				ct.ThrowIfCancellationRequested();
				if (InRemovedFolder(item.Path))
				{
					Step();
					continue;
				}
				if (TryRecordedFile(gameDir, item.Path, out string fullPath) && File.Exists(fullPath))
				{
					fileTransaction.Backup(fullPath, item.Path, isDirectory: false);
					list.Add(fullPath);
				}
				else if (!PackPaths.IsValidRecordedPackFile(item.Path))
				{
					_log.Warn("Recorded path ignored (not a pack path): " + item.Path);
				}
				Step();
			}
			if (RecordsMelonFolder(installRecord))
			{
				string text = PackPaths.FindChild(gameDir, "MelonLoader", directory: true);
				if (text != null)
				{
					fileTransaction.Backup(text, "MelonLoader", isDirectory: true);
				}
			}
			foreach (string item2 in installRecord?.Loader?.Items ?? Array.Empty<string>())
			{
				if (!item2.Equals("MelonLoader/", StringComparison.OrdinalIgnoreCase) && !Brand.Game.ProxyDllNames.Contains(item2, StringComparer.OrdinalIgnoreCase))
				{
					_log.Warn("Recorded MelonLoader item ignored: " + item2);
				}
			}
			Step();
			foreach (var (backupRelative, fullPath2) in ProxyFiles.Find(gameDir))
			{
				fileTransaction.Backup(fullPath2, backupRelative, isDirectory: false);
			}
			foreach (var (text2, fullPath3) in ProxyFiles.Find(_paths.StashDir))
			{
				fileTransaction.Backup(fullPath3, "_stashed/" + text2, isDirectory: false);
			}
			Step();
			foreach (string item3 in userDataFolders)
			{
				ct.ThrowIfCancellationRequested();
				string text3 = PackPaths.ResolveInside(gameDir, item3);
				if (Directory.Exists(text3))
				{
					fileTransaction.Backup(text3, item3, isDirectory: true);
				}
			}
			Step();
			string text4 = PackPaths.ResolveInside(gameDir, "UserData/ActualArrow");
			if (Directory.Exists(text4))
			{
				fileTransaction.Backup(text4, "UserData/ActualArrow", isDirectory: true);
			}
			fileTransaction.Checkpoint("uninstall-done");
			Step();
		}
		catch (Exception ex)
		{
			throw RollbackAndWrap(fileTransaction, null, ex, "uninstall");
		}
		fileTransaction.Complete();
		foreach (string item4 in list)
		{
			PruneEmptyParents(gameDir, item4);
		}
		string[] array = new string[3] { "Mods", "Plugins", "UserLibs" };
		foreach (string text5 in array)
		{
			string text6 = PackPaths.FindChild(gameDir, text5, directory: true);
			if (text6 != null && FsOps.DeleteIfNoFiles(text6))
			{
				_log.Info("Empty folder removed: " + text5 + "/");
			}
		}
		string text7 = PackPaths.FindChild(gameDir, "UserData", directory: true);
		if (text7 != null && FsOps.DeleteIfNoFiles(text7))
		{
			_log.Info("Empty folder removed: UserData/");
		}
		FsOps.TryDeleteFile(_paths.CachedManifestFile);
		FsOps.TryDeleteFile(_paths.CachedPreviewFile);
		if (fileTransaction.BackupDir != null)
		{
			DropRegenerableLoaderParts(fileTransaction.BackupDir);
			if (userDataFolders.Count > 0)
			{
				Backups.MarkKept(fileTransaction.BackupDir, "mod settings from UserData, removed by «Удалить» with the UserData box ticked");
			}
		}
		Backups.Prune();
		_log.Info($"Uninstalled: {fileTransaction.RemovedCount} items" + ((fileTransaction.BackupDir == null) ? "." : (", backup " + fileTransaction.BackupDir + ".")));
		return new UninstallResult(fileTransaction.RemovedCount, fileTransaction.BackupDir);
		bool InRemovedFolder(string rel)
		{
			return userDataFolders.Any((string d) => rel.StartsWith(d + "/", StringComparison.OrdinalIgnoreCase));
		}
		void Step()
		{
			IProgress<OperationProgress>? progress3 = progress;
			if (progress3 != null)
			{
				int? itemsDone2 = Math.Min(++done, total);
				progress3.Report(new OperationProgress(ItemsTotal: total, Kind: OperationKind.Uninstalling, BytesDone: null, BytesTotal: null, ItemsDone: itemsDone2));
			}
		}
	}

	private Exception RollbackAndWrap(FileTransaction tx, string? staging, Exception ex, string what)
	{
		bool flag = tx.Rollback();
		if (staging != null)
		{
			FsOps.TryDeleteDirectory(staging);
			string? directoryName = Path.GetDirectoryName(staging);
			FsOps.PruneEmptyUpwards(directoryName, Path.GetDirectoryName(Path.GetDirectoryName(directoryName)));
		}
		if (!flag)
		{
			_log.Error(what + ": rollback failed after an error", ex);
			return new InstallException(InstallFailure.RollbackFailed, what + " failed and the rollback was incomplete: " + ex.Message, ex);
		}
		if (!(ex is OperationCanceledException))
		{
			if (!(ex is LauncherException))
			{
				if (ex is InvalidDataException)
				{
					_log.Warn(what + ": pack data is broken (rolled back)", ex);
					return new InstallException(InstallFailure.InvalidPack, "Pack data is broken: " + ex.Message, ex);
				}
				_log.Error(what + " failed and was rolled back", ex);
				return new InstallException(InstallFailure.RolledBack, what + " failed and was rolled back: " + ex.Message, ex);
			}
			_log.Warn(what + ": " + ex.Message + " (rolled back)");
			return ex;
		}
		_log.Info(what + ": cancelled and rolled back.");
		return ex;
	}

	private void DropRegenerableLoaderParts(string backupDir)
	{
		string text = Path.Combine(backupDir, "MelonLoader");
		if (!Directory.Exists(text))
		{
			return;
		}
		List<string> list = new List<string>();
		string[] array = new string[2] { "Il2CppAssemblies", "Logs" };
		foreach (string text2 in array)
		{
			string text3 = PackPaths.FindChild(text, text2, directory: true);
			if (text3 != null && FsOps.TryDeleteDirectory(text3))
			{
				list.Add(text2 + "/");
			}
		}
		string text4 = FindRelative(text, "Dependencies/Il2CppAssemblyGenerator");
		if (text4 != null && Directory.Exists(text4))
		{
			foreach (string item in Directory.EnumerateDirectories(text4))
			{
				if (FsOps.TryDeleteDirectory(item))
				{
					list.Add("Dependencies/Il2CppAssemblyGenerator/" + Path.GetFileName(item) + "/");
				}
			}
		}
		if (list.Count > 0)
		{
			_log.Info("Left out of the backup (MelonLoader rebuilds them): " + string.Join(", ", list));
		}
	}

	private void CacheManifest(PackArchive archive)
	{
		try
		{
			Directory.CreateDirectory(_paths.CacheDir);
			byte[] manifestBytes = archive.ManifestBytes;
			if (manifestBytes != null)
			{
				FsOps.WriteAllBytesAtomic(_paths.CachedManifestFile, manifestBytes);
			}
			else
			{
				FsOps.TryDeleteFile(_paths.CachedManifestFile);
			}
			byte[] array = archive.ReadPreview();
			if (array != null)
			{
				FsOps.WriteAllBytesAtomic(_paths.CachedPreviewFile, array);
			}
			else
			{
				FsOps.TryDeleteFile(_paths.CachedPreviewFile);
			}
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException) ? true : false)
		{
			_log.Warn("Could not cache mod.json/preview.png", ex);
		}
	}

	private static void PruneEmptyParents(string gameDir, string removedFile)
	{
		string relativePath = Path.GetRelativePath(gameDir, removedFile);
		_003C_003Ey__InlineArray2<char> buffer = default;
		buffer[0] = Path.DirectorySeparatorChar;
		buffer[1] = Path.AltDirectorySeparatorChar;
		string path = relativePath.Split(buffer)[0];
		string directoryName = Path.GetDirectoryName(removedFile);
		if (directoryName != null)
		{
			FsOps.PruneEmptyUpwards(directoryName, Path.Combine(gameDir, path));
		}
	}

	private static bool TryRecordedFile(string gameDir, string relative, out string fullPath)
	{
		fullPath = "";
		if (PackPaths.IsValidRecordedPackFile(relative))
		{
			return PackPaths.TryResolveInside(gameDir, relative, out fullPath);
		}
		return false;
	}

	private static string? UserDataFolderOf(string rel)
	{
		string[] array = rel.Split('/');
		if (array.Length < 3 || !(array[0] == "UserData"))
		{
			return null;
		}
		return array[0] + "/" + array[1];
	}

	internal static bool RecordsMelonFolder(InstallRecord? record)
	{
		if ((object)record == null)
		{
			return false;
		}
		return record.Loader?.Items.Any((string i) => i.Equals("MelonLoader/", StringComparison.OrdinalIgnoreCase)) == true;
	}
}
