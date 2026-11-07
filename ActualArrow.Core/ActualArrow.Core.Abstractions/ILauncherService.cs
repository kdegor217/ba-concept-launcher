using System;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Abstractions;

public interface ILauncherService
{
	LauncherState State { get; }

	event EventHandler<LauncherState>? StateChanged;

	event EventHandler<ModLogOutcome>? ModLogWatchEnded;

	Task<OperationResult> RefreshAsync(CancellationToken ct = default(CancellationToken));

	Task<OperationResult> PlayModdedAsync(CancellationToken ct = default(CancellationToken));

	Task<OperationResult> PlayVanillaAsync(CancellationToken ct = default(CancellationToken));

	Task<OperationResult> VerifyFilesAsync(CancellationToken ct = default(CancellationToken));

	Task<OperationResult> UninstallAsync(CancellationToken ct = default(CancellationToken));

	Task<OperationResult> ReinstallMelonLoaderAsync(CancellationToken ct = default(CancellationToken));

	void CancelCurrentOperation();

	Task<SettingsApplyResult> ApplySettingsAsync(SettingsChange change, CancellationToken ct = default(CancellationToken));

	void DismissBanner();

	void OpenGameFolder();

	void OpenModLog();

	void OpenLauncherLog();

	Task<bool> StashLoaderIfIdleAsync(CancellationToken ct = default(CancellationToken));

	Task<string?> GameDirToWatchAsync(CancellationToken ct = default(CancellationToken));

	bool IsGameRunning()
	{
		return false;
	}

	Task<bool> WatchGameAndStashAsync(string gameDir, Func<bool> launcherOpen, CancellationToken ct = default(CancellationToken));
}
