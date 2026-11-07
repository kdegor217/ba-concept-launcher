using System;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Abstractions;

public interface IMelonLoaderInstaller
{
	Task<MelonLoaderStatus> GetStatusAsync(string gameDir, CancellationToken ct = default(CancellationToken));

	Task InstallAsync(string gameDir, bool replaceUnrecordedFolder, IProgress<OperationProgress>? progress = null, CancellationToken ct = default(CancellationToken));
}
