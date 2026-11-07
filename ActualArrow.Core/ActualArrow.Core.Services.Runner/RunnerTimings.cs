using System;

namespace ActualArrow.Core.Services.Runner;

public sealed record RunnerTimings
{
	public static RunnerTimings Default { get; } = new RunnerTimings();

	public TimeSpan SteamStartTimeout { get; init; } = Brand.Steam.StartTimeout;

	public TimeSpan SteamPollInterval { get; init; } = TimeSpan.FromMilliseconds(500L);

	public TimeSpan SteamSettleDelay { get; init; } = TimeSpan.FromSeconds(5L);

	public TimeSpan ModLogPollInterval { get; init; } = TimeSpan.FromSeconds(1L);

	public TimeSpan GameExitGrace { get; init; } = TimeSpan.FromSeconds(5L);

	public TimeSpan LogTimeTolerance { get; init; } = TimeSpan.FromSeconds(1L);
}
