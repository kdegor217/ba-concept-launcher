namespace ActualArrow.Core.Models;

public sealed record PlayPlan(PlayPlanKind Kind, string? TargetTag = null, long DownloadBytes = 0L)
{
	public static PlayPlan Unavailable { get; } = new PlayPlan(PlayPlanKind.Unavailable, null, 0L);

	public static PlayPlan NeedsGameDir { get; } = new PlayPlan(PlayPlanKind.Unavailable, null, 0L)
	{
		UnavailableReasonKey = "Play.NeedsGameDir"
	};

	public static PlayPlan NeedsGitHub { get; } = new PlayPlan(PlayPlanKind.Unavailable, null, 0L)
	{
		UnavailableReasonKey = "Play.NeedsGitHub"
	};

	public string? UnavailableReasonKey { get; init; }

	public StatusMessage Subtitle
	{
		get
		{
			switch (Kind)
			{
			case PlayPlanKind.Ready:
				return StatusMessage.Of("Play.AllReady");
			case PlayPlanKind.Install:
			case PlayPlanKind.Repair:
				return StatusMessage.Of("Play.WillDownload", DownloadBytes);
			case PlayPlanKind.Update:
				return StatusMessage.Of("Play.UpdateTo", TargetTag ?? "");
			case PlayPlanKind.Rollback:
				return StatusMessage.Of("Play.RollbackTo", TargetTag ?? "");
			default:
				return StatusMessage.Of(UnavailableReasonKey ?? "Play.Unavailable");
			}
		}
	}
}
