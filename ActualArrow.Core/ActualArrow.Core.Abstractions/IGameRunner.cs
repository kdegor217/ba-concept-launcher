using System;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Abstractions;

public interface IGameRunner
{
	bool IsDryRun { get; }

	bool VanillaThroughSteam => false;

	bool IsSteamRunning();

	bool IsGameRunning();

	Task<bool> EnsureSteamRunningAsync(IProgress<TimeSpan>? waited = null, CancellationToken ct = default(CancellationToken));

	Task<LaunchResult> LaunchModdedAsync(string gameDir, CancellationToken ct = default(CancellationToken));

	Task<LaunchResult> LaunchVanillaAsync(string gameDir, CancellationToken ct = default(CancellationToken));

	Task<ModLogOutcome> WatchModLogAsync(string gameDir, DateTimeOffset launchedUtc, TimeSpan timeout, CancellationToken ct = default(CancellationToken));

	void OpenInShell(string pathOrUri);
}
