using System;

namespace ActualArrow.Core.Models;

public sealed record ReleaseInfo(string Repo, string Tag, string? Name, DateTimeOffset? PublishedUtc, Uri? HtmlUrl, ReleaseAsset Asset)
{
	public string? Notes { get; init; }
}
