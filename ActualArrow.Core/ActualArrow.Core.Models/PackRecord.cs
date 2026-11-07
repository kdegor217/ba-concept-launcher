using System;
using System.Collections.Generic;

namespace ActualArrow.Core.Models;

public sealed record PackRecord
{
	public required string Repo { get; init; }

	public required string Tag { get; init; }

	public PackManifest Manifest { get; init; } = PackManifest.Empty;

	public required string AssetName { get; init; }

	public required string AssetSha256 { get; init; }

	public DateTimeOffset InstalledUtc { get; init; }

	public IReadOnlyList<InstalledFile> Files { get; init; } = Array.Empty<InstalledFile>();

	public IReadOnlyList<string> CreatedUserDataFolders { get; init; } = Array.Empty<string>();
}
