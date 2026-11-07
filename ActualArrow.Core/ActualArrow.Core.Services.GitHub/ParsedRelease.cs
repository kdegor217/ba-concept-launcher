using ActualArrow.Core.Models;

namespace ActualArrow.Core.Services.GitHub;

internal readonly record struct ParsedRelease(ReleaseInfo? Release, ReleaseFeedError? Error)
{
	public bool IsReleaseDocument
	{
		get
		{
			if ((object)Release == null)
			{
				return Error is ReleaseFeedError.NoPackAsset;
			}
			return true;
		}
	}
}
