using System.Collections.Generic;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Services.Runner;

internal static class StateBuilder
{
	public static ReleaseFeedResult? ReleaseFor(ReleaseFeedResult? result, string repo)
	{
		ReleaseInfo releaseInfo = result?.Release;
		if ((object)releaseInfo == null || TagText.SameRepo(releaseInfo.Repo, repo))
		{
			return result;
		}
		return null;
	}

	public static PackAction DecidePack(PackHealth? health, ReleaseFeedResult? release, string repo, IComparer<string?> tags)
	{
		PackRecord packRecord = health?.Record?.Pack;
		ReleaseInfo releaseInfo = ReleaseFor(release, repo)?.Release;
		if ((object)packRecord == null)
		{
			if ((object)releaseInfo != null)
			{
				return PackAction.Install;
			}
			return PackAction.Blocked;
		}
		if (!TagText.SameRepo(packRecord.Repo, repo))
		{
			if ((object)releaseInfo != null)
			{
				return PackAction.Install;
			}
			return PackAction.Blocked;
		}
		if ((object)releaseInfo == null)
		{
			if (!health.IsIntact)
			{
				return PackAction.Blocked;
			}
			return PackAction.None;
		}
		if (TagText.Same(packRecord.Tag, releaseInfo.Tag))
		{
			if (!health.IsIntact)
			{
				return PackAction.Repair;
			}
			return PackAction.None;
		}
		if (tags.Compare(releaseInfo.Tag, packRecord.Tag) < 0)
		{
			return PackAction.Rollback;
		}
		return PackAction.Update;
	}

	public static StatusMessage ErrorMessage(ReleaseFeedError error)
	{
		if (error is ReleaseFeedError.RateLimited { ResetUtc: { } resetUtc })
		{
			return StatusMessage.Of(error.MessageKey, resetUtc);
		}
		return StatusMessage.Of(error.MessageKey);
	}

	public static LauncherState Build(RefreshSnapshot s, IComparer<string?> tags)
	{
		ReleaseFeedResult releaseFeedResult = ReleaseFor(s.Release, s.Repo);
		ReleaseInfo releaseInfo = releaseFeedResult?.Release;
		bool flag = s.Game?.IsValid ?? false;
		PackRecord packRecord = s.Health?.Record?.Pack;
		bool flag2 = (object)packRecord != null && !TagText.SameRepo(packRecord.Repo, s.Repo);
		if (flag2)
		{
			packRecord = null;
		}
		bool flag3 = flag && (object)packRecord != null;
		LauncherState launcherState = new LauncherState
		{
			Phase = ((!flag) ? LauncherPhase.GameNotFound : LauncherPhase.Ready),
			Game = s.Game,
			DetectedGameDir = s.DetectedGameDir,
			PackRepo = s.Repo,
			InstalledTag = (flag3 ? packRecord.Tag : null),
			LatestRelease = releaseInfo,
			Manifest = (flag3 ? packRecord.Manifest : (flag2 ? null : s.CachedManifest)),
			PreviewImagePath = (flag2 ? null : s.PreviewPath),
			MelonLoader = ((!flag) ? ((MelonLoaderState?)null) : s.MelonLoader?.State),
			CanOpenModLog = (flag && s.ModLogExists)
		};
		if (!flag)
		{
			StatusMessage detail = (((object)s.Game == null) ? null : StatusMessage.Of("Error.GameDirInvalid"));
			return launcherState with
			{
				PackStatus = (((object)releaseInfo != null || (object)releaseFeedResult?.Error == null) ? PackStatusKind.NotInstalled : PackStatusKind.Error),
				PlayPlan = PlayPlan.NeedsGameDir,
				Banner = new Banner(BannerSeverity.Warning, StatusMessage.Of("Notice.GameNotFound"), detail)
			};
		}
		MelonLoaderStatus? melonLoader = s.MelonLoader;
		long num = (((object)melonLoader != null && melonLoader.IsReady) ? 0 : 20155622);
		long num2 = releaseInfo?.Asset.Size ?? 0;
		PackAction packAction = DecidePack(s.Health, releaseFeedResult, s.Repo, tags);
		ProxyState proxies = s.Proxies;
		bool flag4 = (object)proxies != null && (proxies.InRoot.Count > 0 || proxies.Stashed.Count > 0);
		MelonLoaderStatus melonLoader2 = s.MelonLoader;
		bool flag5 = (object)melonLoader2 != null && (melonLoader2.State != MelonLoaderState.NotInstalled || melonLoader2.FolderExists || melonLoader2.RecordedTag != null);
		launcherState = launcherState with
		{
			CanPlayVanilla = true,
			CanVerify = flag3,
			CanUninstall = (flag3 | flag5 | flag4)
		};
		Banner banner = null;
		ReleaseFeedError releaseFeedError = releaseFeedResult?.Error;
		if ((object)releaseFeedError != null)
		{
			banner = (flag3 ? new Banner(BannerSeverity.Warning, StatusMessage.Of("Notice.UsingInstalled", packRecord.Tag), ErrorMessage(releaseFeedError)) : new Banner(BannerSeverity.Error, ErrorMessage(releaseFeedError)));
		}
		switch (packAction)
		{
		case PackAction.Install:
			return launcherState with
			{
				PackStatus = PackStatusKind.NotInstalled,
				PlayPlan = new PlayPlan(PlayPlanKind.Install, releaseInfo.Tag, num2 + num),
				CanPlayModded = true,
				Banner = banner
			};
		case PackAction.Update:
			return launcherState with
			{
				PackStatus = PackStatusKind.UpdateAvailable,
				PlayPlan = new PlayPlan(PlayPlanKind.Update, releaseInfo.Tag, num2 + num),
				CanPlayModded = true,
				Banner = banner
			};
		case PackAction.Rollback:
			return launcherState with
			{
				PackStatus = PackStatusKind.Rollback,
				PlayPlan = new PlayPlan(PlayPlanKind.Rollback, releaseInfo.Tag, num2 + num),
				CanPlayModded = true,
				Banner = banner
			};
		case PackAction.Repair:
			return launcherState with
			{
				PackStatus = PackStatusKind.UpToDate,
				PlayPlan = new PlayPlan(PlayPlanKind.Repair, releaseInfo.Tag, num2 + num),
				CanPlayModded = true,
				Banner = banner
			};
		case PackAction.None:
			return launcherState with
			{
				PackStatus = (((object)releaseInfo == null) ? PackStatusKind.InstalledUnchecked : PackStatusKind.UpToDate),
				PlayPlan = ((num == 0L) ? new PlayPlan(PlayPlanKind.Ready, packRecord.Tag, 0L) : new PlayPlan(PlayPlanKind.Repair, packRecord.Tag, num)),
				CanPlayModded = true,
				Banner = banner
			};
		default:
			if (flag3)
			{
				return launcherState with
				{
					PackStatus = PackStatusKind.InstalledUnchecked,
					PlayPlan = new PlayPlan(PlayPlanKind.Repair, packRecord.Tag, num),
					CanPlayModded = true,
					Banner = banner
				};
			}
			return launcherState with
			{
				PackStatus = (((object)releaseFeedResult?.Error == null) ? PackStatusKind.NotInstalled : PackStatusKind.Error),
				PlayPlan = (((object)releaseFeedResult?.Error != null) ? PlayPlan.NeedsGitHub : PlayPlan.Unavailable),
				CanPlayModded = false,
				Banner = banner
			};
		}
	}
}
