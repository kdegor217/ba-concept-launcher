using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Abstractions;

public interface IGameLocator
{
	IReadOnlyList<string> FindSteamRoots();

	Task<string?> DetectAsync(CancellationToken ct = default(CancellationToken));

	GameDirValidation Validate(string? gameDir);
}
