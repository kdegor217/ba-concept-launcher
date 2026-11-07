using System;
using System.IO;
using ActualArrow.Core.Abstractions;
using ActualArrow.Core.Services.Basics;

namespace ActualArrow.Core.Services;

public sealed class AppPaths : IAppPaths
{
	internal const string SettingsName = "settings.json";

	internal const string LogName = "launcher.log";

	internal const string StashName = "stashed-proxies";

	internal const string DownloadsName = "downloads";

	internal const string CacheName = "cache";

	internal const string BackupName = "backup";

	public string Root { get; }

	public string SettingsFile { get; }

	public string LogFile { get; }

	public string StashDir { get; }

	public string DownloadsDir { get; }

	public string CacheDir { get; }

	public string BackupDir { get; }

	public string CachedManifestFile { get; }

	public string CachedPreviewFile { get; }

	public AppPaths(string? rootOverride = null)
	{
		Root = (string.IsNullOrWhiteSpace(rootOverride) ? DefaultRoot() : PathText.ToFullPath(PathText.ExpandHome(rootOverride.Trim())));
		SettingsFile = Path.Combine(Root, "settings.json");
		LogFile = Path.Combine(Root, "launcher.log");
		StashDir = Path.Combine(Root, "stashed-proxies");
		DownloadsDir = Path.Combine(Root, "downloads");
		CacheDir = Path.Combine(Root, "cache");
		BackupDir = Path.Combine(Root, "backup");
		CachedManifestFile = Path.Combine(CacheDir, "mod.json");
		CachedPreviewFile = Path.Combine(CacheDir, "preview.png");
	}

	public static string DefaultRoot()
	{
		return Path.Combine(PerUserConfigBase(), "ActualArrowLauncher");
	}

	public void EnsureCreated()
	{
		string[] array = new string[5] { Root, StashDir, DownloadsDir, CacheDir, BackupDir };
		for (int i = 0; i < array.Length; i++)
		{
			Directory.CreateDirectory(array[i]);
		}
	}

	private static string PerUserConfigBase()
	{
		if (OperatingSystem.IsWindows())
		{
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify);
			if (!string.IsNullOrEmpty(folderPath))
			{
				return folderPath;
			}
			string environmentVariable = Environment.GetEnvironmentVariable("APPDATA");
			if (!string.IsNullOrEmpty(environmentVariable))
			{
				return environmentVariable;
			}
			return Path.Combine(PathText.HomeDirectory(), "AppData", "Roaming");
		}
		string environmentVariable2 = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
		if (!string.IsNullOrWhiteSpace(environmentVariable2) && Path.IsPathFullyQualified(environmentVariable2))
		{
			return environmentVariable2;
		}
		return Path.Combine(PathText.HomeDirectory(), ".config");
	}
}
