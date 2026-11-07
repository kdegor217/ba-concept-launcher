using System;
using System.IO;
using System.Security;

namespace ActualArrow.Core.Services.Basics;

internal static class PathText
{
	public static StringComparer OsComparer { get; } = (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

	public static string HomeDirectory()
	{
		string text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
		if (string.IsNullOrEmpty(text))
		{
			text = Environment.GetEnvironmentVariable("HOME");
		}
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		return Environment.CurrentDirectory;
	}

	public static string ExpandHome(string path)
	{
		if (path == "~")
		{
			return HomeDirectory();
		}
		if (path.Length >= 2 && path[0] == '~' && (path[1] == '/' || path[1] == '\\'))
		{
			return Path.Combine(HomeDirectory(), path.Substring(2, path.Length - 2));
		}
		return path;
	}

	public static string ToFullPath(string path)
	{
		return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
	}

	public static string? TryFullPath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}
		try
		{
			return ToFullPath(path.Trim());
		}
		catch (Exception ex) when ((ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException || ex is SecurityException) ? true : false)
		{
			return null;
		}
	}
}
