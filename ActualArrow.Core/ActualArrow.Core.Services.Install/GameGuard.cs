using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Services.Install;

internal static class GameGuard
{
	public static bool IsGameProcessRunning()
	{
		try
		{
			Process[] processesByName = Process.GetProcessesByName("BrokenArrow");
			Process[] array = processesByName;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].Dispose();
			}
			return processesByName.Length != 0;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
		catch (Win32Exception)
		{
			return false;
		}
	}

	public static void EnsureNotRunning(Func<bool> isRunning)
	{
		if (isRunning())
		{
			throw new LauncherException("Status.CloseGame", "The game is running; close it before installing or removing mods.", null);
		}
	}

	public static string RequireGameDir(string gameDir)
	{
		if (string.IsNullOrWhiteSpace(gameDir))
		{
			throw new LauncherException("Error.GameDirInvalid", "Game folder is not set.", null);
		}
		string text = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameDir));
		if (!Directory.Exists(text))
		{
			throw new LauncherException("Error.GameDirInvalid", "Game folder does not exist: " + text, null);
		}
		return text;
	}
}
