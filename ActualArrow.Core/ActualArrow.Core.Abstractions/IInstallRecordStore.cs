using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Abstractions;

public interface IInstallRecordStore
{
	string GetPath(string gameDir);

	Task<InstallRecord?> ReadAsync(string gameDir, CancellationToken ct = default(CancellationToken));

	Task WriteAsync(string gameDir, InstallRecord record, CancellationToken ct = default(CancellationToken));
}
