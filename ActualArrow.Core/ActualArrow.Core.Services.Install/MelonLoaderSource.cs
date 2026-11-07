using System;

namespace ActualArrow.Core.Services.Install;

public sealed record MelonLoaderSource(Uri Url, string FileName, long Size, string Sha256)
{
	public static MelonLoaderSource Pinned { get; } = new MelonLoaderSource(new Uri("https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip"), "MelonLoader.x64.zip", 20155622L, "5b2b2f3d1cd42b59ec886c5bdc2663edae87a0097a4f4a8f58c0965a99dda416");
}
