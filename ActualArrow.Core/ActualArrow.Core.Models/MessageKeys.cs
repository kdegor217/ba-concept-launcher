namespace ActualArrow.Core.Models;

public static class MessageKeys
{
	public const string StatusReady = "Status.Ready";

	public const string StatusChecking = "Status.Checking";

	public const string StatusDownloadingPack = "Status.DownloadingPack";

	public const string StatusDownloadingMelonLoader = "Status.DownloadingMelonLoader";

	public const string StatusInstallingMelonLoader = "Status.InstallingMelonLoader";

	public const string StatusInstallingPack = "Status.InstallingPack";

	public const string StatusInstalled = "Status.Installed";

	public const string StatusRollingBack = "Status.RollingBack";

	public const string StatusVerifying = "Status.Verifying";

	public const string StatusVerifyOk = "Status.VerifyOk";

	public const string StatusVerifyRepaired = "Status.VerifyRepaired";

	public const string StatusUninstalling = "Status.Uninstalling";

	public const string StatusUninstalled = "Status.Uninstalled";

	public const string StatusWaitingForSteam = "Status.WaitingForSteam";

	public const string StatusLaunching = "Status.Launching";

	public const string StatusLaunchedModded = "Status.LaunchedModded";

	public const string StatusLaunchedVanilla = "Status.LaunchedVanilla";

	public const string StatusDryRun = "Status.DryRun";

	public const string StatusModsLoaded = "Status.ModsLoaded";

	public const string StatusModsNoResponse = "Status.ModsNoResponse";

	public const string StatusCloseGame = "Status.CloseGame";

	public const string StatusAntiCheatStarting = "Status.AntiCheatStarting";

	public const string StatusNothingToUninstall = "Status.NothingToUninstall";

	public const string StatusCancelled = "Status.Cancelled";

	public const string PackUpToDate = "Pack.UpToDate";

	public const string PackUpdateAvailable = "Pack.UpdateAvailable";

	public const string PackRollback = "Pack.Rollback";

	public const string PackNotInstalled = "Pack.NotInstalled";

	public const string PackUnknown = "Pack.Unknown";

	public const string PackError = "Pack.Error";

	public const string PlayWillDownload = "Play.WillDownload";

	public const string PlayUpdateTo = "Play.UpdateTo";

	public const string PlayRollbackTo = "Play.RollbackTo";

	public const string PlayAllReady = "Play.AllReady";

	public const string PlayUnavailable = "Play.Unavailable";

	public const string PlayNeedsGameDir = "Play.NeedsGameDir";

	public const string PlayNeedsGitHub = "Play.NeedsGitHub";

	public const string NoticeUsingInstalled = "Notice.UsingInstalled";

	public const string NoticeFirstModdedStart = "Notice.FirstModdedStart";

	public const string NoticeGameNotFound = "Notice.GameNotFound";

	public const string ErrorReleaseNotFound = "Error.ReleaseNotFound";

	public const string ErrorRateLimited = "Error.RateLimited";

	public const string ErrorOffline = "Error.Offline";

	public const string ErrorNoPackAsset = "Error.NoPackAsset";

	public const string ErrorReleaseOther = "Error.ReleaseOther";

	public const string ErrorDownloadFailed = "Error.DownloadFailed";

	public const string ErrorSizeMismatch = "Error.SizeMismatch";

	public const string ErrorHashMismatch = "Error.HashMismatch";

	public const string ErrorInvalidPack = "Error.InvalidPack";

	public const string ErrorInstallFailed = "Error.InstallFailed";

	public const string ErrorRollbackFailed = "Error.RollbackFailed";

	public const string ErrorNotInstalled = "Error.NotInstalled";

	public const string ErrorMelonLoaderFailed = "Error.MelonLoaderFailed";

	public const string ErrorGameDirInvalid = "Error.GameDirInvalid";

	public const string ErrorSteamNotStarted = "Error.SteamNotStarted";

	public const string ErrorLaunchFailed = "Error.LaunchFailed";

	public const string ErrorDownloadStalled = "Error.DownloadStalled";

	public const string ErrorProxyLeft = "Error.ProxyLeft";

	public const string ErrorForeignLoader = "Error.ForeignLoader";

	public const string ErrorNoAntiCheatLauncher = "Error.NoAntiCheatLauncher";

	public const string ErrorProtonMissing = "Error.ProtonMissing";

	public const string ErrorUnderWine = "Error.UnderWine";

	public const string ErrorNotSteamFolder = "Error.NotSteamFolder";

	public const string ErrorRecordUnreadable = "Error.RecordUnreadable";

	public const string ErrorUnexpected = "Error.Unexpected";

	public const string SettingsGameDirInvalid = "Settings.GameDirInvalid";

	public const string SettingsRepoInvalid = "Settings.RepoInvalid";
}
