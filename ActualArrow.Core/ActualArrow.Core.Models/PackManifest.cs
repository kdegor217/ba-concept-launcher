namespace ActualArrow.Core.Models;

public sealed record PackManifest
{
	public string? Id { get; init; }

	public string? Name { get; init; }

	public string? Version { get; init; }

	public string? Author { get; init; }

	public string? Description { get; init; }

	public string? GameVersion { get; init; }

	public static PackManifest Empty { get; } = new PackManifest();
}
