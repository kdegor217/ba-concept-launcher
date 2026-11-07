using System.Collections.Generic;
using System.IO;

namespace ActualArrow.Core.Services.Basics;

internal static class SteamRootProbe
{
	public static IReadOnlyList<string> Candidates(SteamProbeEnvironment env)
	{
		List<string> list = new List<string>();
		if (env.IsWindows)
		{
			Add(list, env.ReadRegistry(arg1: false, "Software\\Valve\\Steam", "SteamPath"));
			Add(list, env.ReadRegistry(arg1: true, "SOFTWARE\\WOW6432Node\\Valve\\Steam", "InstallPath"));
			if (!string.IsNullOrWhiteSpace(env.ProgramFilesX86))
			{
				Add(list, Path.Combine(env.ProgramFilesX86, "Steam"));
			}
			if (!string.IsNullOrWhiteSpace(env.WineUnixHome))
			{
				string text = "Z:" + env.WineUnixHome.TrimEnd('/').Replace('/', '\\');
				foreach (string item in Brand.Steam.LinuxRootsRelativeToHome)
				{
					Add(list, text + "\\" + item.Replace('/', '\\'));
				}
			}
		}
		else if (!string.IsNullOrWhiteSpace(env.Home))
		{
			foreach (string item2 in Brand.Steam.LinuxRootsRelativeToHome)
			{
				Add(list, Path.Combine(env.Home, item2));
			}
		}
		return list;
	}

	private static void Add(List<string> list, string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			list.Add(value.Trim().Trim('"'));
		}
	}
}
