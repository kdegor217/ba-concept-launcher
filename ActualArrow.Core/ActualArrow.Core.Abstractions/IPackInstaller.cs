using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Abstractions;

public interface IPackInstaller
{
	Task<PackInspection> InspectAsync(string packPath, CancellationToken ct = default(CancellationToken));

	Task<PackHealth> CheckQuickAsync(string gameDir, CancellationToken ct = default(CancellationToken));

	Task<PackVerifyResult> VerifyAsync(string gameDir, IProgress<OperationProgress>? progress = null, CancellationToken ct = default(CancellationToken));

	Task<IReadOnlyList<string>> FindForeignModsAsync(string gameDir, PackInspection pack, CancellationToken ct = default(CancellationToken));

	Task<PackInstallResult> InstallAsync(PackInstallRequest request, IProgress<OperationProgress>? progress = null, CancellationToken ct = default(CancellationToken));

	Task<UninstallPlan> GetUninstallPlanAsync(string gameDir, CancellationToken ct = default(CancellationToken));

	Task<UninstallResult> UninstallAsync(string gameDir, UninstallOptions options, IProgress<OperationProgress>? progress = null, CancellationToken ct = default(CancellationToken));
}
