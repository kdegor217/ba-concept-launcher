namespace ActualArrow.Core.Models;

public enum PackStatusKind
{
	Unknown,
	NotInstalled,
	UpToDate,
	UpdateAvailable,
	Rollback,
	InstalledUnchecked,
	Error
}
