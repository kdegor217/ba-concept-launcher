using System;
using System.IO;
using System.Linq;

namespace ActualArrow.Core.Services.Install;

internal static class FsOps
{
	public static void MoveFile(string from, string to, bool overwrite = false)
	{
		EnsureParent(to);
		ClearReadOnly(from);
		File.Move(from, to, overwrite);
		if (!File.Exists(from) || SamePath(from, to))
		{
			return;
		}
		try
		{
			ClearReadOnly(from);
			File.Delete(from);
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			if (!overwrite)
			{
				TryDeleteFile(to);
			}
			throw new IOException($"{from} was copied to {to} but could not be removed: {ex.Message}", ex);
		}
		if (!File.Exists(from))
		{
			return;
		}
		throw new IOException(from + " is still there after moving it to " + to);
	}

	public static void MoveDirectory(string from, string to)
	{
		EnsureParent(to);
		if (Directory.Exists(to) || File.Exists(to))
		{
			throw new IOException("Destination already exists: " + to);
		}
		if (SameRoot(from, to))
		{
			try
			{
				Directory.Move(from, to);
				return;
			}
			catch (IOException ex) when (IsCrossVolume(ex) && Directory.Exists(from) && !Directory.Exists(to))
			{
			}
		}
		try
		{
			CopyDirectory(from, to);
		}
		catch
		{
			TryDeleteDirectory(to);
			throw;
		}
		try
		{
			DeleteDirectory(from);
		}
		catch (Exception ex2) when ((ex2 is IOException || ex2 is UnauthorizedAccessException) ? true : false)
		{
			try
			{
				CopyMissing(to, from);
			}
			catch (Exception ex3) when ((ex3 is IOException || ex3 is UnauthorizedAccessException) ? true : false)
			{
				throw new IncompleteMoveException($"Moving {from} to {to}: the source could only be partly removed ({ex2.Message}) and could not be restored ({ex3.Message}). The full copy is at {to}.", ex2);
			}
			TryDeleteDirectory(to);
			throw new IOException("Could not move " + from + ": " + ex2.Message, ex2);
		}
	}

	internal static bool IsCrossVolume(IOException ex)
	{
		if (ex.HResult != -2147024879)
		{
			if (!OperatingSystem.IsWindows())
			{
				return ex.HResult == 18;
			}
			return false;
		}
		return true;
	}

	internal static bool SameRoot(string a, string b)
	{
		return string.Equals(Path.GetPathRoot(Path.GetFullPath(a)), Path.GetPathRoot(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase);
	}

	private static bool SamePath(string a, string b)
	{
		return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), PackPaths.FsComparison);
	}

	private static void ClearReadOnly(string file)
	{
		try
		{
			FileAttributes attributes = File.GetAttributes(file);
			if ((attributes & FileAttributes.ReadOnly) != 0)
			{
				File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
			}
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
		}
	}

	private static void CopyMissing(string from, string to)
	{
		Directory.CreateDirectory(to);
		foreach (string item in Directory.EnumerateFiles(from))
		{
			string text = Path.Combine(to, Path.GetFileName(item));
			if (!File.Exists(text))
			{
				File.Copy(item, text, overwrite: false);
			}
		}
		foreach (string item2 in Directory.EnumerateDirectories(from))
		{
			CopyMissing(item2, Path.Combine(to, Path.GetFileName(item2)));
		}
	}

	public static void CopyDirectory(string from, string to)
	{
		Directory.CreateDirectory(to);
		foreach (string item in Directory.EnumerateFiles(from))
		{
			File.Copy(item, Path.Combine(to, Path.GetFileName(item)), overwrite: false);
		}
		foreach (string item2 in Directory.EnumerateDirectories(from))
		{
			CopyDirectory(item2, Path.Combine(to, Path.GetFileName(item2)));
		}
	}

	public static void DeleteDirectory(string dir)
	{
		if (!Directory.Exists(dir))
		{
			return;
		}
		foreach (string item in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
		{
			FileAttributes attributes = File.GetAttributes(item);
			if ((attributes & FileAttributes.ReadOnly) != 0)
			{
				File.SetAttributes(item, attributes & ~FileAttributes.ReadOnly);
			}
		}
		Directory.Delete(dir, recursive: true);
	}

	public static bool TryDeleteDirectory(string dir)
	{
		try
		{
			DeleteDirectory(dir);
			return true;
		}
		catch (IOException)
		{
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}
	}

	public static bool TryDeleteFile(string file)
	{
		try
		{
			if (File.Exists(file))
			{
				File.Delete(file);
			}
			return true;
		}
		catch (IOException)
		{
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}
	}

	public static bool HasNoFiles(string dir)
	{
		if (Directory.Exists(dir))
		{
			return !Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Any();
		}
		return false;
	}

	public static bool DeleteIfNoFiles(string dir)
	{
		try
		{
			if (!HasNoFiles(dir))
			{
				return false;
			}
			Directory.Delete(dir, recursive: true);
			return true;
		}
		catch (IOException)
		{
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}
	}

	public static void PruneEmptyUpwards(string startDir, string stopAt)
	{
		string text = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stopAt));
		string text2 = Path.TrimEndingDirectorySeparator(Path.GetFullPath(startDir));
		while (text2.Length > text.Length && text2.StartsWith(text + Path.DirectorySeparatorChar, PackPaths.FsComparison))
		{
			try
			{
				if (!Directory.Exists(text2) || Directory.EnumerateFileSystemEntries(text2).Any())
				{
					break;
				}
				Directory.Delete(text2);
			}
			catch (IOException)
			{
				break;
			}
			catch (UnauthorizedAccessException)
			{
				break;
			}
			text2 = Path.GetDirectoryName(text2) ?? text;
		}
	}

	public static void WriteAllBytesAtomic(string path, ReadOnlySpan<byte> bytes)
	{
		EnsureParent(path);
		string text = path + ".tmp-" + Guid.NewGuid().ToString("N").Substring(0, 8);
		try
		{
			using (FileStream fileStream = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{
				fileStream.Write(bytes);
				fileStream.Flush(flushToDisk: true);
			}
			File.Move(text, path, overwrite: true);
		}
		finally
		{
			TryDeleteFile(text);
		}
	}

	private static void EnsureParent(string path)
	{
		string directoryName = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
	}
}
