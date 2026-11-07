using ActualArrow.Core.Models;

namespace ActualArrow.Core.Services.Runner;

internal sealed record RefreshSnapshot(GameLocation? Game, string? DetectedGameDir, string Repo, PackHealth? Health, MelonLoaderStatus? MelonLoader, ProxyState? Proxies, ReleaseFeedResult? Release, PackManifest? CachedManifest, string? PreviewPath, bool ModLogExists);
