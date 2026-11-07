using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Abstractions;

public interface IPackCatalog
{
	Task<IReadOnlyList<PackCatalogEntry>> GetAsync(CancellationToken ct = default(CancellationToken));
}
