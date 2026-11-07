using System.Collections.Generic;

namespace ActualArrow.Core.Models;

public sealed record PackInstallResult(InstallRecord Record, int Added, int Changed, int Removed, int Unchanged, string? BackupDir, IReadOnlyList<SkippedEntry> Skipped);
