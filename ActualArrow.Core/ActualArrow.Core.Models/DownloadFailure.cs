namespace ActualArrow.Core.Models;

public enum DownloadFailure
{
	Network,
	Timeout,
	HttpStatus,
	SizeMismatch,
	HashMismatch,
	HostNotAllowed,
	Io
}
