using System;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Abstractions;

public interface IDownloader
{
	Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default(CancellationToken));
}
