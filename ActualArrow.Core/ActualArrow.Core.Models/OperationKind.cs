namespace ActualArrow.Core.Models;

public enum OperationKind
{
	CheckingRelease,
	DownloadingMelonLoader,
	InstallingMelonLoader,
	DownloadingPack,
	InstallingPack,
	Verifying,
	Uninstalling,
	WaitingForSteam,
	Launching
}
