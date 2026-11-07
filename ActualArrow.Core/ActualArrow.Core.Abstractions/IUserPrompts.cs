using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Abstractions;

public interface IUserPrompts
{
	Task<bool> ConfirmBackupForeignModsAsync(IReadOnlyList<string> modNames, CancellationToken ct = default(CancellationToken));

	Task<bool> ConfirmReplaceUnrecordedMelonLoaderAsync(string folderPath, CancellationToken ct = default(CancellationToken));

	Task<UninstallOptions?> ConfirmUninstallAsync(UninstallPlan plan, CancellationToken ct = default(CancellationToken));

	Task<HelpDialogChoice> ShowModsDidNotStartAsync(CancellationToken ct = default(CancellationToken));
}
