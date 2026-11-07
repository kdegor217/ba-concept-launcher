using System;

namespace ActualArrow.Core.Services.GitHub;

internal static class Sha256Hex
{
	private const string Prefix = "sha256:";

	public static string? FromDigest(string? digest)
	{
		if (string.IsNullOrWhiteSpace(digest))
		{
			return null;
		}
		string text = digest.Trim();
		if (!text.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		string text2 = text;
		int length = "sha256:".Length;
		return NormalizeHex(text2.Substring(length, text2.Length - length));
	}

	public static string? Normalize(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		string text = value.Trim();
		if (text.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
		{
			string text2 = text;
			int length = "sha256:".Length;
			text = text2.Substring(length, text2.Length - length);
		}
		return NormalizeHex(text);
	}

	private static string? NormalizeHex(string hex)
	{
		hex = hex.Trim();
		if (hex.Length != 64)
		{
			return null;
		}
		string text = hex;
		for (int i = 0; i < text.Length; i++)
		{
			if (!Uri.IsHexDigit(text[i]))
			{
				return null;
			}
		}
		return hex.ToLowerInvariant();
	}
}
