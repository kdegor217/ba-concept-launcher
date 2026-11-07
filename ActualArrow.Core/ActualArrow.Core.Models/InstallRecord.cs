namespace ActualArrow.Core.Models;

public sealed record InstallRecord
{
	public int Schema { get; init; } = 1;

	public PackRecord? Pack { get; init; }

	public LoaderInstall? Loader { get; init; }

	public const int CurrentSchema = 1;
}
