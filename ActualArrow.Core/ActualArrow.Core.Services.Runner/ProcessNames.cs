using System;

namespace ActualArrow.Core.Services.Runner;

public static class ProcessNames
{
	public static string Normalize(string name)
	{
		string text = name.Trim();
		if (!text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
		{
			return text;
		}
		string text2 = text;
		return text2.Substring(0, text2.Length - 4);
	}

	public static bool Same(string a, string b)
	{
		return string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
	}
}
