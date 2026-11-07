using System;
using System.Collections.Generic;

namespace ActualArrow.Core.Models;

public sealed record PackInstallRequest
{
	public required string GameDir { get; init; }

	public required string PackPath { get; init; }

	public required string Repo { get; init; }

	public required string Tag { get; init; }

	public required string AssetName { get; init; }

	public required string AssetSha256 { get; init; }

	public IReadOnlyList<string> ForeignMods { get; init; } = Array.Empty<string>();

	public ForeignModsAction ForeignModsAction { get; init; }

	public bool ForceRewrite { get; init; }
}
