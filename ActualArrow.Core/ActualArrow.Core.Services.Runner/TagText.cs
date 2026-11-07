using System;

namespace ActualArrow.Core.Services.Runner;

internal static class TagText
{
	public static bool Same(string? a, string? b)
	{
		return VersionComparer.SameTag(a, b);
	}

	public static bool SameRepo(string? a, string? b)
	{
		if (a != null && b != null)
		{
			return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}
}
