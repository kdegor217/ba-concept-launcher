using System.Collections.Generic;

namespace ActualArrow.Core.Models;

public sealed record UninstallPlan(string GameDir, int PackFileCount, bool HasMelonLoader, IReadOnlyList<string> Proxies, IReadOnlyList<string> UserDataFolders)
{
	public bool IsEmpty
	{
		get
		{
			if (PackFileCount == 0 && !HasMelonLoader)
			{
				return Proxies.Count == 0;
			}
			return false;
		}
	}
}
