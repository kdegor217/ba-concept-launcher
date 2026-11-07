using System;

namespace ActualArrow.Core.Models;

public sealed record LauncherSettings
{
	public string? GameDir { get; init; }

	public string? PackRepo { get; init; }

	public string Language { get; init; } = "ru";

	public bool CloseOnLaunch { get; init; }

	public string? LastEtag { get; init; }

	public string? LastReleaseJson { get; init; }

	public string? LastReleaseRepo { get; init; }

	public DateTimeOffset? LastCheckUtc { get; init; }

	public string EffectivePackRepo
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(PackRepo))
			{
				return PackRepo;
			}
			return "wow-png/Leha_Arrow_mods";
		}
	}
}
