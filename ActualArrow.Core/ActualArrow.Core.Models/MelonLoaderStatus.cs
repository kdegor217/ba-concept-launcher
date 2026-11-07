namespace ActualArrow.Core.Models;

public sealed record MelonLoaderStatus(MelonLoaderState State, string? RecordedTag, bool FolderExists, bool ProxyInRoot, bool ProxyStashed)
{
	public bool IsReady => State == MelonLoaderState.Ready;
}
