using System;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Abstractions;

public interface ISettingsStore
{
	LauncherSettings Current { get; }

	event EventHandler<LauncherSettings>? Changed;

	Task<LauncherSettings> LoadAsync(CancellationToken ct = default(CancellationToken));

	Task<LauncherSettings> UpdateAsync(Func<LauncherSettings, LauncherSettings> change, CancellationToken ct = default(CancellationToken));
}
