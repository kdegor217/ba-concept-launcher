using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Abstractions;
using ActualArrow.Core.Models;
using ActualArrow.Core.Services.Basics;

namespace ActualArrow.Core.Services;

public sealed class GameLocator : IGameLocator
{
	private sealed record Library(string Path, bool ListsGame);

	private readonly ILog _log;

	private readonly IReadOnlyList<string>? _rootsOverride;

	private readonly SteamProbeEnvironment? _environment;

	public GameLocator(ILog log, IReadOnlyList<string>? steamRootsOverride = null)
	{
		_log = log ?? throw new ArgumentNullException("log");
		_rootsOverride = steamRootsOverride;
	}

	internal GameLocator(ILog log, SteamProbeEnvironment environment)
	{
		_log = log ?? throw new ArgumentNullException("log");
		_environment = environment ?? throw new ArgumentNullException("environment");
	}

	public IReadOnlyList<string> FindSteamRoots()
	{
		IReadOnlyList<string>? obj = _rootsOverride ?? SteamRootProbe.Candidates(_environment ?? SteamProbeEnvironment.Current());
		List<string> list = new List<string>();
		HashSet<string> hashSet = new HashSet<string>(PathText.OsComparer);
		foreach (string item in obj)
		{
			string text = PathText.TryFullPath(item);
			if (text != null && SafeDirectoryExists(text) && hashSet.Add(IdentityOf(text)))
			{
				list.Add(text);
			}
		}
		return list;
	}

	public async Task<string?> DetectAsync(CancellationToken ct = default(CancellationToken))
	{
		IReadOnlyList<string> readOnlyList = FindSteamRoots();
		if (readOnlyList.Count == 0)
		{
			_log.Info("game detect: no Steam folder found");
			return null;
		}
		List<Library> libraries = await CollectLibrariesAsync(readOnlyList, ct).ConfigureAwait(continueOnCapturedContext: false);
		List<(Library, string, int)> list = new List<(Library, string, int)>();
		foreach (Library item2 in libraries)
		{
			string text = FindChildDirectory(item2.Path, "steamapps");
			string text2 = ((text == null) ? null : FindChildFile(text, "appmanifest_1604270.acf"));
			int item = ((!item2.ListsGame) ? ((text2 != null) ? 1 : 2) : 0);
			list.Add((item2, text2, item));
		}
		foreach (var (lib, text3, _) in list.OrderBy(((Library Lib, string Manifest, int Rank) r) => r.Rank))
		{
			ct.ThrowIfCancellationRequested();
			string text4 = ((text3 != null) ? (await ReadInstallDirAsync(text3, ct).ConfigureAwait(continueOnCapturedContext: false)) : null);
			string name = text4 ?? "broken_arrow";
			string text5 = FindChildDirectory(lib.Path, "steamapps");
			string text6 = ((text5 == null) ? null : FindChildDirectory(text5, "common"));
			string text7 = ((text6 == null) ? null : FindChildDirectory(text6, name));
			if (text7 != null)
			{
				GameDirValidation gameDirValidation = Validate(text7);
				if (gameDirValidation == GameDirValidation.Valid)
				{
					_log.Info("game detect: found " + text7);
					return text7;
				}
				_log.Debug($"game detect: {text7} skipped ({gameDirValidation})");
			}
		}
		_log.Info($"game detect: not found in {libraries.Count} Steam librar{((libraries.Count == 1) ? "y" : "ies")}");
		return null;
	}

	internal static string WinePath(string path)
	{
		if (!OperatingSystem.IsWindows() || !path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal))
		{
			return path;
		}
		return "Z:" + path.Replace('/', '\\');
	}

	public GameDirValidation Validate(string? gameDir)
	{
		if (string.IsNullOrWhiteSpace(gameDir))
		{
			return GameDirValidation.Empty;
		}
		string text = PathText.TryFullPath(gameDir);
		if (text == null || !SafeDirectoryExists(text))
		{
			return GameDirValidation.NotFound;
		}
		try
		{
			return (!File.Exists(Path.Combine(text, "BrokenArrow.exe"))) ? GameDirValidation.NoGameExe : GameDirValidation.Valid;
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) ? true : false)
		{
			return GameDirValidation.NoGameExe;
		}
	}

	private async Task<List<Library>> CollectLibrariesAsync(IReadOnlyList<string> roots, CancellationToken ct)
	{
		List<Library> result = new List<Library>();
		Dictionary<string, int> index = new Dictionary<string, int>(PathText.OsComparer);
		foreach (string root in roots)
		{
			ct.ThrowIfCancellationRequested();
			string text = FindChildDirectory(root, "steamapps");
			string text2 = ((text == null) ? null : FindChildFile(text, Path.GetFileName("steamapps/libraryfolders.vdf")));
			if (text2 != null)
			{
				foreach (SteamLibrary item in await ReadLibraryFoldersAsync(text2, ct).ConfigureAwait(continueOnCapturedContext: false))
				{
					Add(item.Path, item.ListsGame);
				}
			}
			Add(root, listsGame: false);
		}
		return result;
		void Add(string path, bool listsGame)
		{
			string text3 = PathText.TryFullPath(WinePath(path));
			if (text3 != null && SafeDirectoryExists(text3))
			{
				string key = IdentityOf(text3);
				if (index.TryGetValue(key, out var value))
				{
					if (listsGame && !result[value].ListsGame)
					{
						result[value] = result[value] with
						{
							ListsGame = true
						};
					}
				}
				else
				{
					index[key] = result.Count;
					result.Add(new Library(text3, listsGame));
				}
			}
		}
	}

	private async Task<IReadOnlyList<SteamLibrary>> ReadLibraryFoldersAsync(string file, CancellationToken ct)
	{
		try
		{
			return SteamLibraries.FromLibraryFolders(VdfParser.Parse(await File.ReadAllTextAsync(file, ct).ConfigureAwait(continueOnCapturedContext: false)), "1604270");
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is FormatException) ? true : false)
		{
			_log.Warn("game detect: skipping unreadable " + file + ": " + ex.Message);
			return Array.Empty<SteamLibrary>();
		}
	}

	private async Task<string?> ReadInstallDirAsync(string manifest, CancellationToken ct)
	{
		try
		{
			string? text = SteamLibraries.InstallDirFromManifest(VdfParser.Parse(await File.ReadAllTextAsync(manifest, ct).ConfigureAwait(continueOnCapturedContext: false)));
			if (text == null)
			{
				_log.Warn($"game detect: {manifest} has no usable installdir; trying '{"broken_arrow"}'");
			}
			return text;
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is FormatException) ? true : false)
		{
			_log.Warn("game detect: skipping unreadable " + manifest + ": " + ex.Message);
			return null;
		}
	}

	private static string IdentityOf(string fullPath)
	{
		try
		{
			FileSystemInfo fileSystemInfo = new DirectoryInfo(fullPath).ResolveLinkTarget(returnFinalTarget: true);
			if (fileSystemInfo != null)
			{
				return PathText.ToFullPath(fileSystemInfo.FullName);
			}
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
		}
		return fullPath;
	}

	private static bool SafeDirectoryExists(string path)
	{
		try
		{
			return Directory.Exists(path);
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) ? true : false)
		{
			return false;
		}
	}

	internal static string? FindChildDirectory(string parent, string name)
	{
		try
		{
			string text = Path.Combine(parent, name);
			if (Directory.Exists(text))
			{
				return text;
			}
			if (!Directory.Exists(parent))
			{
				return null;
			}
			return Directory.EnumerateDirectories(parent).FirstOrDefault((string d) => string.Equals(Path.GetFileName(d), name, StringComparison.OrdinalIgnoreCase));
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) ? true : false)
		{
			return null;
		}
	}

	internal static string? FindChildFile(string parent, string name)
	{
		try
		{
			string text = Path.Combine(parent, name);
			if (File.Exists(text))
			{
				return text;
			}
			if (!Directory.Exists(parent))
			{
				return null;
			}
			return Directory.EnumerateFiles(parent).FirstOrDefault((string f) => string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase));
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) ? true : false)
		{
			return null;
		}
	}
}
