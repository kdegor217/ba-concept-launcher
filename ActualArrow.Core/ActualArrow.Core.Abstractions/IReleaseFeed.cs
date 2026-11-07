using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Abstractions;

public interface IReleaseFeed
{
	Task<ReleaseFeedResult> GetLatestAsync(string repo, CancellationToken ct = default(CancellationToken));
}
