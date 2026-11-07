namespace ActualArrow.Core.Services.Install;

internal readonly record struct EntryVerdict(EntryKind Kind, string? RelativePath, string? Reason)
{
	public static EntryVerdict Reject(string reason)
	{
		return new EntryVerdict(EntryKind.Rejected, null, reason);
	}
}
