namespace ActualArrow.Core.Models;

public sealed record ReleaseFeedResult
{
	public ReleaseInfo? Release { get; init; }

	public ReleaseFeedError? Error { get; init; }

	public bool FromCache { get; init; }

	public bool IsSuccess
	{
		get
		{
			if ((object)Release != null)
			{
				return (object)Error == null;
			}
			return false;
		}
	}

	public static ReleaseFeedResult Ok(ReleaseInfo release, bool fromCache = false)
	{
		return new ReleaseFeedResult
		{
			Release = release,
			FromCache = fromCache
		};
	}

	public static ReleaseFeedResult Fail(ReleaseFeedError error)
	{
		return new ReleaseFeedResult
		{
			Error = error
		};
	}
}
