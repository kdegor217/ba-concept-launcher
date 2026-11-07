namespace ActualArrow.Core.Models;

public sealed record LauncherState
{
	public LauncherPhase Phase { get; init; }

	public GameLocation? Game { get; init; }

	public string? DetectedGameDir { get; init; }

	public string PackRepo { get; init; } = "wow-png/Leha_Arrow_mods";

	public PackStatusKind PackStatus { get; init; }

	public string? InstalledTag { get; init; }

	public ReleaseInfo? LatestRelease { get; init; }

	public string? LatestTag => LatestRelease?.Tag;

	public PackManifest? Manifest { get; init; }

	public string? PreviewImagePath { get; init; }

	public MelonLoaderState? MelonLoader { get; init; }

	public PlayPlan PlayPlan { get; init; } = ActualArrow.Core.Models.PlayPlan.Unavailable;

	public bool CanPlayModded { get; init; }

	public bool CanPlayVanilla { get; init; }

	public bool CanVerify { get; init; }

	public bool CanUninstall { get; init; }

	public bool CanOpenModLog { get; init; }

	public OperationKind? CurrentOperation { get; init; }

	public OperationProgress? Progress { get; init; }

	public bool CanCancel { get; init; }

	public bool IsBusy => CurrentOperation.HasValue;

	public StatusMessage? Status { get; init; }

	public Banner? Banner { get; init; }
}
