namespace ActualArrow.Core.Models;

public sealed record DownloadResult(string Path, long Size, string Sha256);
