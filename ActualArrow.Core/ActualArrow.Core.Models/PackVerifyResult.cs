using System.Collections.Generic;

namespace ActualArrow.Core.Models;

public sealed record PackVerifyResult(int Checked, IReadOnlyList<string> Missing, IReadOnlyList<string> Changed)
{
	public bool IsOk
	{
		get
		{
			if (Missing.Count == 0)
			{
				return Changed.Count == 0;
			}
			return false;
		}
	}
}
