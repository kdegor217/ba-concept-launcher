using System.Collections.Generic;

namespace ActualArrow.Core.Models;

public sealed record StatusMessage(string Key, IReadOnlyList<object> Args)
{
	public static StatusMessage Of(string key, params object[] args)
	{
		return new StatusMessage(key, args);
	}

	public override string ToString()
	{
		if (Args.Count != 0)
		{
			return Key + "(" + string.Join(", ", Args) + ")";
		}
		return Key;
	}
}
