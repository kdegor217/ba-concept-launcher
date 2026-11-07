using System;

namespace ActualArrow.Core.Models;

public abstract record ReleaseFeedError(string? Detail)
{
	public sealed record NotFound(string? Detail = null) : ReleaseFeedError(Detail)
	{
		public override string MessageKey => "Error.ReleaseNotFound";
	}

	public sealed record RateLimited(DateTimeOffset? ResetUtc, string? Detail = null) : ReleaseFeedError(Detail)
	{
		public override string MessageKey => "Error.RateLimited";
	}

	public sealed record Offline(string? Detail = null) : ReleaseFeedError(Detail)
	{
		public override string MessageKey => "Error.Offline";
	}

	public sealed record NoPackAsset(string? Detail = null) : ReleaseFeedError(Detail)
	{
		public override string MessageKey => "Error.NoPackAsset";
	}

	public sealed record Other(int? StatusCode, string? Detail = null) : ReleaseFeedError(Detail)
	{
		public override string MessageKey => "Error.ReleaseOther";
	}

	public abstract string MessageKey { get; }
}
