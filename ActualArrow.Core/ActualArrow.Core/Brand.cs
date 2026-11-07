using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ActualArrow.Core;

public static class Brand
{
	public static class Catalog
	{
		public const string Repo = "aurka1823/ActualArrow";

		public const string FilePath = "catalog.json";

		public const int MaxPacks = 20;

		public const int MaxBytes = 65536;
	}

	public static class Game
	{
		public const string SteamAppId = "1604270";

		public const string FolderName = "broken_arrow";

		public const string ExeName = "BrokenArrow.exe";

		public const string ProcessName = "BrokenArrow";

		public const string EacLauncherExeName = "EACLauncher.exe";

		public const string EacLauncherProcessName = "EACLauncher";

		public const string AppManifestFileName = "appmanifest_1604270.acf";

		public static readonly IReadOnlyList<string> PackRoots = new _003C_003Ez__ReadOnlyArray<string>(new string[4] { "Mods", "Plugins", "UserLibs", "UserData" });

		public static readonly IReadOnlyList<string> ProxyDllNames = new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "version.dll", "winhttp.dll" });

		public static readonly IReadOnlyList<string> OtherLoaderProxyNames = new _003C_003Ez__ReadOnlyArray<string>(new string[13]
		{
			"winmm.dll", "dinput.dll", "dinput8.dll", "dsound.dll", "ddraw.dll", "msacm32.dll", "dxgi.dll", "d3d8.dll", "d3d9.dll", "d3d10.dll",
			"d3d11.dll", "d3d12.dll", "opengl32.dll"
		});

		public const string MelonProxyDll = "version.dll";

		public const string LauncherDataRelative = "UserData/ActualArrow";

		public const string InstallRecordRelative = "UserData/ActualArrow/install.json";

		public const string StagingRelative = "UserData/ActualArrow/staging";

		public const string MelonLoaderFolder = "MelonLoader";

		public const string ModLogRelative = "MelonLoader/Latest.log";

		public const string Il2CppAssembliesRelative = "MelonLoader/Il2CppAssemblies";

		public static readonly IReadOnlyList<string> ModdedArguments = new _003C_003Ez__ReadOnlySingleElementList<string>("--melonloader.hideconsole");

		public static readonly IReadOnlyDictionary<string, string> LaunchEnvironment = new Dictionary<string, string>
		{
			["SteamAppId"] = "1604270",
			["SteamGameId"] = "1604270"
		};

		public static bool IsFirstModdedStart(string gameDir)
		{
			try
			{
				string path = Path.Combine(gameDir, "MelonLoader/Il2CppAssemblies".Replace('/', Path.DirectorySeparatorChar));
				return !Directory.Exists(path) || !Directory.EnumerateFiles(path, "*.dll").Any();
			}
			catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
			{
				return false;
			}
		}
	}

	public static class Steam
	{
		public const string OpenMainUri = "steam://open/main";

		public const string RunGameUri = "steam://rungameid/1604270";

		public const string ProcessName = "steam";

		public static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(90L);

		public const string RegistryUserKey = "Software\\Valve\\Steam";

		public const string RegistryUserValue = "SteamPath";

		public const string RegistryMachineKey = "SOFTWARE\\WOW6432Node\\Valve\\Steam";

		public const string RegistryMachineValue = "InstallPath";

		public const string LibraryFoldersRelative = "steamapps/libraryfolders.vdf";

		public static readonly IReadOnlyList<string> LinuxRootsRelativeToHome = new _003C_003Ez__ReadOnlyArray<string>(new string[3] { ".local/share/Steam", ".steam/steam", ".var/app/com.valvesoftware.Steam/.local/share/Steam" });
	}

	public static class MelonLoader
	{
		public const string Repo = "LavaGang/MelonLoader";

		public const string Tag = "v0.7.3";

		public const string AssetName = "MelonLoader.x64.zip";

		public const string DownloadUrl = "https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip";

		public const long Size = 20155622L;

		public const string Sha256 = "5b2b2f3d1cd42b59ec886c5bdc2663edae87a0097a4f4a8f58c0965a99dda416";
	}

	public static class GitHub
	{
		public const string ApiBase = "https://api.github.com/";

		public const string AcceptHeader = "application/vnd.github+json";

		public static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(15L);

		public static readonly TimeSpan PlayCheckTimeout = TimeSpan.FromSeconds(4L);

		public static readonly TimeSpan FreshCheckAge = TimeSpan.FromMinutes(2L);

		public static readonly IReadOnlyList<string> AllowedHosts = new _003C_003Ez__ReadOnlyArray<string>(new string[4] { "api.github.com", "github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com" });

		public static string LatestReleasePath(string repo)
		{
			return "repos/" + repo + "/releases/latest";
		}
	}

	public static class Pack
	{
		public const string Extension = ".bamod";

		public const string FallbackExtension = ".zip";

		public const string ManifestEntry = "mod.json";

		public const string PreviewEntry = "preview.png";
	}

	public static class Links
	{
		public const string VcRedistPage = "https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist";
	}

	public static class Limits
	{
		public const long LogRotateBytes = 1048576L;

		public const int BackupsToKeep = 3;

		public static readonly TimeSpan ModLogWatch = TimeSpan.FromSeconds(90L);

		public static readonly TimeSpan VanillaStartGuard = TimeSpan.FromSeconds(60L);
	}

	public const string ProductName = "ActualArrow";

	public const string DefaultPackRepo = "wow-png/Leha_Arrow_mods";

	public const string AppDataFolderName = "ActualArrowLauncher";

	public const string SingleInstanceName = "ActualArrowLauncher.Instance";

	public const string UserAgentProduct = "ActualArrowLauncher";

	public const string RepoPattern = "^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})/[A-Za-z0-9._-]{1,100}$";

	public const string DefaultLanguage = "ru";

	public static readonly IReadOnlyList<string> Languages = new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "ru", "en" });

	public static string Version { get; } = ReadVersion();

	public static string UserAgent => "ActualArrowLauncher/" + Version;

	private static string ReadVersion()
	{
		Assembly assembly = typeof(Brand).Assembly;
		string text = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
		if (!string.IsNullOrWhiteSpace(text))
		{
			int num = text.IndexOf('+');
			if (num < 0)
			{
				return text;
			}
			return text.Substring(0, num);
		}
		return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
	}
}
