using System;

namespace ActualArrow.Core.Models;

public sealed record DownloadRequest(Uri Url, string DestinationPath, long? ExpectedSize = null, string? ExpectedSha256 = null, TimeSpan? StallTimeout = null);
