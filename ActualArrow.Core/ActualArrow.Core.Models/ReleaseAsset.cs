using System;

namespace ActualArrow.Core.Models;

public sealed record ReleaseAsset(string Name, Uri DownloadUrl, long Size, string? Sha256);
