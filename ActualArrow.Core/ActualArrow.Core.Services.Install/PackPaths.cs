using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ActualArrow.Core.Services.Install;

internal static class PackPaths
{
	private static readonly char[] ForbiddenChars = new char[7] { '<', '>', '"', '|', '?', '*', ':' };

	private static readonly HashSet<string> ReservedDeviceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6",
		"COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7",
		"LPT8", "LPT9"
	};

	public static StringComparison FsComparison
	{
		get
		{
			if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
			{
				return StringComparison.Ordinal;
			}
			return StringComparison.OrdinalIgnoreCase;
		}
	}

	public static bool TrySplit(string rawName, out string[] segments, out bool isDirectory, out string reason)
	{
		segments = Array.Empty<string>();
		isDirectory = false;
		reason = "";
		string text = rawName.Replace('\\', '/');
		if (text.Length == 0)
		{
			reason = "empty name";
			return false;
		}
		if (text[0] == '/')
		{
			reason = "absolute path";
			return false;
		}
		if (text.Length >= 2 && char.IsAsciiLetter(text[0]) && text[1] == ':')
		{
			reason = "drive letter";
			return false;
		}
		if (text.EndsWith('/'))
		{
			isDirectory = true;
			string text2 = text;
			text = text2.Substring(0, text2.Length - 1);
			if (text.Length == 0)
			{
				reason = "empty name";
				return false;
			}
		}
		string[] array = text.Split('/');
		string[] array2 = array;
		foreach (string text3 in array2)
		{
			if (text3.Length == 0)
			{
				reason = "empty segment";
				return false;
			}
			if ((text3 == "." || text3 == "..") ? true : false)
			{
				reason = "dot segment";
				return false;
			}
			if (text3.IndexOfAny(ForbiddenChars) >= 0 || text3.Any(char.IsControl))
			{
				reason = "invalid character";
				return false;
			}
			if (text3.EndsWith('.') || text3.EndsWith(' ') || text3.StartsWith(' '))
			{
				reason = "leading/trailing space or trailing dot";
				return false;
			}
			string item = text3.Split('.')[0];
			if (ReservedDeviceNames.Contains(item))
			{
				reason = "reserved device name";
				return false;
			}
		}
		segments = array;
		return true;
	}

	public static EntryVerdict ClassifyPackEntry(string rawName)
	{
		if (!TrySplit(rawName, out string[] segments, out bool isDirectory, out string reason))
		{
			return EntryVerdict.Reject(reason);
		}
		if (segments.Length == 1 && !isDirectory)
		{
			if (segments[0].Equals("mod.json", StringComparison.OrdinalIgnoreCase))
			{
				return new EntryVerdict(EntryKind.Manifest, "mod.json", null);
			}
			if (segments[0].Equals("preview.png", StringComparison.OrdinalIgnoreCase))
			{
				return new EntryVerdict(EntryKind.Preview, "preview.png", null);
			}
		}
		string text = CanonicalRoot(segments[0]);
		if (text == null)
		{
			if (segments.Length != 1 || isDirectory)
			{
				return EntryVerdict.Reject("unknown root");
			}
			return EntryVerdict.Reject("file in game root");
		}
		segments[0] = text;
		if (IsLauncherFolder(segments))
		{
			return EntryVerdict.Reject("launcher folder is reserved");
		}
		string relativePath = string.Join('/', segments);
		if (isDirectory)
		{
			return new EntryVerdict(EntryKind.Directory, relativePath, null);
		}
		if (segments.Length == 1)
		{
			return EntryVerdict.Reject("file in game root");
		}
		return new EntryVerdict(EntryKind.PackFile, relativePath, null);
	}

	public static bool IsValidRecordedPackFile(string relativePath)
	{
		EntryVerdict entryVerdict = ClassifyPackEntry(relativePath);
		if (entryVerdict.Kind == EntryKind.PackFile)
		{
			return string.Equals(entryVerdict.RelativePath, relativePath, StringComparison.Ordinal);
		}
		return false;
	}

	public static bool IsValidUserDataFolder(string relativePath)
	{
		if ((!TrySplit(relativePath, out string[] segments, out bool isDirectory, out string _) | isDirectory) || segments.Length != 2)
		{
			return false;
		}
		if (segments[0] == "UserData")
		{
			return !IsLauncherFolder(segments);
		}
		return false;
	}

	public static string? CanonicalRoot(string segment)
	{
		return Brand.Game.PackRoots.FirstOrDefault((string r) => r.Equals(segment, StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsLauncherFolder(string[] segs)
	{
		string[] array = "UserData/ActualArrow".Split('/');
		if (segs.Length >= array.Length)
		{
			return array.Select((string s, int i) => s.Equals(segs[i], StringComparison.OrdinalIgnoreCase)).All((bool x) => x);
		}
		return false;
	}

	public static bool TryResolveInside(string baseDir, string relative, out string fullPath)
	{
		fullPath = "";
		if (string.IsNullOrEmpty(relative) || Path.IsPathRooted(relative.Replace('/', Path.DirectorySeparatorChar)))
		{
			return false;
		}
		string text = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDir));
		string fullPath2 = Path.GetFullPath(Path.Combine(text, relative.Replace('/', Path.DirectorySeparatorChar)));
		string text2 = text + Path.DirectorySeparatorChar;
		if (!fullPath2.StartsWith(text2, FsComparison) || fullPath2.Length == text2.Length)
		{
			return false;
		}
		fullPath = fullPath2;
		return true;
	}

	public static string ResolveInside(string baseDir, string relative)
	{
		if (!TryResolveInside(baseDir, relative, out string fullPath))
		{
			throw new InvalidDataException($"Path '{relative}' escapes '{baseDir}'.");
		}
		return fullPath;
	}

	public static string? FindChild(string dir, string name, bool directory)
	{
		if (!Directory.Exists(dir))
		{
			return null;
		}
		string text = Path.Combine(dir, name);
		if (directory ? Directory.Exists(text) : File.Exists(text))
		{
			return text;
		}
		try
		{
			return (directory ? Directory.EnumerateDirectories(dir) : Directory.EnumerateFiles(dir)).FirstOrDefault((string p) => Path.GetFileName(p).Equals(name, StringComparison.OrdinalIgnoreCase));
		}
		catch (IOException)
		{
			return null;
		}
		catch (UnauthorizedAccessException)
		{
			return null;
		}
	}
}
