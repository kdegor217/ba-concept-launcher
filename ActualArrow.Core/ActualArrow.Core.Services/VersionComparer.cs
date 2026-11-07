using System;
using System.Collections.Generic;

namespace ActualArrow.Core.Services;

public sealed class VersionComparer : IComparer<string?>
{
	private static readonly char[] Separators = new char[3] { '.', '-', '_' };

	public static VersionComparer Instance { get; } = new VersionComparer();

	public int Compare(string? x, string? y)
	{
		if ((object)x == y)
		{
			return 0;
		}
		if (x == null)
		{
			return -1;
		}
		if (y == null)
		{
			return 1;
		}
		List<string> list = Tokenize(Normalize(x));
		List<string> list2 = Tokenize(Normalize(y));
		int num = Math.Max(list.Count, list2.Count);
		for (int i = 0; i < num; i++)
		{
			if (i >= list.Count)
			{
				return -1;
			}
			if (i >= list2.Count)
			{
				return 1;
			}
			int num2 = CompareToken(list[i], list2[i]);
			if (num2 != 0)
			{
				return num2;
			}
		}
		return 0;
	}

	public static string Normalize(string tag)
	{
		ArgumentNullException.ThrowIfNull(tag, "tag");
		string text = tag.Trim();
		if (text.Length > 0 && (text[0] == 'v' || text[0] == 'V'))
		{
			string text2 = text;
			text = text2.Substring(1, text2.Length - 1).TrimStart();
		}
		return text;
	}

	public static bool SameTag(string? a, string? b)
	{
		if (a != null && b != null)
		{
			return string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal);
		}
		return false;
	}

	private static int CompareToken(string a, string b)
	{
		bool flag = IsDigits(a);
		bool flag2 = IsDigits(b);
		if (flag & flag2)
		{
			return CompareNumbers(a, b);
		}
		if (flag)
		{
			return -1;
		}
		if (flag2)
		{
			return 1;
		}
		return Math.Sign(string.Compare(a, b, StringComparison.OrdinalIgnoreCase));
	}

	private static int CompareNumbers(string a, string b)
	{
		a = a.TrimStart('0');
		b = b.TrimStart('0');
		if (a.Length != b.Length)
		{
			if (a.Length >= b.Length)
			{
				return 1;
			}
			return -1;
		}
		return Math.Sign(string.CompareOrdinal(a, b));
	}

	private static bool IsDigits(string s)
	{
		foreach (char c in s)
		{
			if ((c < '0' || c > '9') ? true : false)
			{
				return false;
			}
		}
		return s.Length > 0;
	}

	internal static List<string> Tokenize(string normalized)
	{
		List<string> list = new List<string>();
		string[] array = normalized.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		foreach (string text in array)
		{
			int num = 0;
			for (int j = 1; j <= text.Length; j++)
			{
				if (j == text.Length || IsAsciiDigit(text[j]) != IsAsciiDigit(text[j - 1]))
				{
					int num2 = num;
					list.Add(text.Substring(num2, j - num2));
					num = j;
				}
			}
		}
		return list;
	}

	private static bool IsAsciiDigit(char c)
	{
		if (c >= '0')
		{
			return c <= '9';
		}
		return false;
	}
}
