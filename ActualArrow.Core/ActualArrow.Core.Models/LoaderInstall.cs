using System;
using System.Collections.Generic;

namespace ActualArrow.Core.Models;

public sealed record LoaderInstall
{
	public required string Tag { get; init; }

	public DateTimeOffset InstalledUtc { get; init; }

	public IReadOnlyList<string> Items { get; init; } = Array.Empty<string>();
}
