using System.Collections.Generic;
using System.Linq;

namespace ActualArrow.Core.Models;

public sealed record PackInspection(string PackPath, PackManifest Manifest, IReadOnlyList<PackEntry> Entries, IReadOnlyList<SkippedEntry> Skipped, bool HasPreview)
{
	public long TotalSize => Entries.Sum((PackEntry e) => e.Size);
}
