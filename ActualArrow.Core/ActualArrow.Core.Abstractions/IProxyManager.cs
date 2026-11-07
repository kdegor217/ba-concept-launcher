using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Abstractions;

public interface IProxyManager
{
	ProxyState GetState(string gameDir);

	Task<IReadOnlyList<string>> StashAllAsync(string gameDir, CancellationToken ct = default(CancellationToken));

	Task<bool> RestoreMelonProxyAsync(string gameDir, CancellationToken ct = default(CancellationToken));

	Task<bool> StashMelonProxyAsync(string gameDir, CancellationToken ct = default(CancellationToken));
}
