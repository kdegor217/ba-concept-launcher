namespace ActualArrow.Core.Abstractions;

public interface IAppPaths
{
	string Root { get; }

	string SettingsFile { get; }

	string LogFile { get; }

	string StashDir { get; }

	string DownloadsDir { get; }

	string CacheDir { get; }

	string BackupDir { get; }

	string CachedManifestFile { get; }

	string CachedPreviewFile { get; }

	void EnsureCreated();
}
