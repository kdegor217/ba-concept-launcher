using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ActualArrow.Core.Abstractions;

namespace ActualArrow.Core.Services.Runner;

public static class ProtonLaunch
{
	public const string Verb = "waitforexitandrun";

	private static readonly Regex CommandLine = new Regex("\"commandline\"\\s+\"([^\"]*)\"", RegexOptions.CultureInvariant);

	private static readonly Regex RequireTool = new Regex("\"require_tool_appid\"\\s+\"(\\d+)\"", RegexOptions.CultureInvariant);

	private static readonly Regex InstallDir = new Regex("\"installdir\"\\s+\"([^\"]*)\"", RegexOptions.CultureInvariant);

	public static ProcessLaunchSpec? Build(string gameDir, IReadOnlyList<string> steamRoots, ILog log, out string? problem, IReadOnlyList<string>? gameArguments = null)
	{
		problem = null;
		string directoryName = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(gameDir));
		string text = ((directoryName == null) ? null : Path.GetDirectoryName(directoryName));
		if (text == null || !string.Equals(Path.GetFileName(directoryName), "common", StringComparison.OrdinalIgnoreCase))
		{
			return Fail(out problem, gameDir + " is not inside <library>/steamapps/common");
		}
		string text2 = Path.Combine(text, "compatdata", "1604270");
		string text3 = Path.Combine(text2, "config_info");
		if (!File.Exists(text3))
		{
			return Fail(out problem, "no " + text3 + ": the game was never started through Proton by Steam");
		}
		string text4 = ProtonDirFrom(File.ReadAllLines(text3));
		if (text4 == null || !File.Exists(Path.Combine(text4, "proton")))
		{
			return Fail(out problem, $"the Proton named in {text3} is not installed ({text4 ?? "no path"})");
		}
		string text5 = ReadText(Path.Combine(text4, "toolmanifest.vdf"));
		string text6 = steamRoots.FirstOrDefault((string r) => Directory.Exists(Path.Combine(r, "steamapps")));
		if (text6 == null)
		{
			return Fail(out problem, "no Steam client folder");
		}
		List<string> list = new List<string>();
		List<string> list2 = new List<string> { text4 };
		Match match = RequireTool.Match(text5);
		string fileName;
		if (match != null && match.Success)
		{
			string value = match.Groups[1].Value;
			List<string> list3 = new List<string>();
			list3.Add(text);
			list3.AddRange(steamRoots.Select((string r) => Path.Combine(r, "steamapps")));
			string text7 = FindTool(value, new _003C_003Ez__ReadOnlyList<string>(list3));
			if (text7 == null)
			{
				return Fail(out problem, "Proton needs Steam tool " + match.Groups[1].Value + " (Steam Linux Runtime), which is not installed");
			}
			List<string> list4 = Tokens(ReadText(Path.Combine(text7, "toolmanifest.vdf")), text7);
			if (list4.Count == 0 || !File.Exists(list4[0]))
			{
				return Fail(out problem, text7 + " has no usable entry point");
			}
			fileName = list4[0];
			list.AddRange(list4.Skip(1));
			list2.Add(text7);
			List<string> list5 = Tokens(text5, text4);
			List<string> list6;
			if (list5.Count <= 0)
			{
				int num = 2;
				list6 = new List<string>(num);
				CollectionsMarshal.SetCount(list6, num);
				Span<string> span = CollectionsMarshal.AsSpan(list6);
				int num2 = 0;
				span[num2] = Path.Combine(text4, "proton");
				num2++;
				span[num2] = "waitforexitandrun";
			}
			else
			{
				list6 = list5;
			}
			list.AddRange(list6);
		}
		else
		{
			List<string> list7 = Tokens(text5, text4);
			if (list7.Count == 0)
			{
				int num2 = 2;
				List<string> list8 = new List<string>(num2);
				CollectionsMarshal.SetCount(list8, num2);
				Span<string> span2 = CollectionsMarshal.AsSpan(list8);
				int num = 0;
				span2[num] = Path.Combine(text4, "proton");
				num++;
				span2[num] = "waitforexitandrun";
				list7 = list8;
			}
			fileName = list7[0];
			list.AddRange(list7.Skip(1));
		}
		list.Add(Path.Combine(gameDir, "BrokenArrow.exe"));
		list.AddRange(gameArguments ?? Array.Empty<string>());
		List<string> values = (from p in new string[1] { text }.Concat(steamRoots.Select((string r) => Path.Combine(r, "steamapps"))).Where(Directory.Exists)
			select Path.GetDirectoryName(p)).Distinct(StringComparer.Ordinal).ToList();
		Dictionary<string, string> environment = new Dictionary<string, string>(Brand.Game.LaunchEnvironment)
		{
			["STEAM_COMPAT_APP_ID"] = "1604270",
			["STEAM_COMPAT_DATA_PATH"] = text2,
			["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = text6,
			["STEAM_COMPAT_INSTALL_PATH"] = gameDir,
			["STEAM_COMPAT_LIBRARY_PATHS"] = string.Join(':', values),
			["STEAM_COMPAT_TOOL_PATHS"] = string.Join(':', list2),
			["WINEDLLOVERRIDES"] = "version=n,b"
		};
		log.Info($"Proton launch: {Path.GetFileName(text4)} for {"1604270"}, prefix {text2}");
		return new ProcessLaunchSpec(fileName, gameDir, environment, list);
	}

	internal static string? ProtonDirFrom(IEnumerable<string> configInfoLines)
	{
		foreach (string configInfoLine in configInfoLines)
		{
			int num = configInfoLine.IndexOf("/files/", StringComparison.Ordinal);
			if (num > 0 && configInfoLine.StartsWith('/'))
			{
				return configInfoLine.Substring(0, num);
			}
		}
		return null;
	}

	internal static List<string> Tokens(string manifest, string toolDir)
	{
		Match match = CommandLine.Match(manifest);
		if (match == null || !match.Success)
		{
			return new List<string>();
		}
		return match.Groups[1].Value.Replace("%verb%", "waitforexitandrun", StringComparison.Ordinal).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select((string t, int i) => (i != 0 || !t.StartsWith('/')) ? t : (toolDir + t))
			.ToList();
	}

	private static string? FindTool(string appId, IEnumerable<string> steamappsDirs)
	{
		foreach (string item in steamappsDirs.Distinct(StringComparer.Ordinal))
		{
			string path = Path.Combine(item, "appmanifest_" + appId + ".acf");
			if (!File.Exists(path))
			{
				continue;
			}
			Match match = InstallDir.Match(ReadText(path));
			if (match != null && match.Success)
			{
				string text = Path.Combine(item, "common", match.Groups[1].Value);
				if (Directory.Exists(text))
				{
					return text;
				}
			}
		}
		return null;
	}

	private static string ReadText(string path)
	{
		try
		{
			return File.Exists(path) ? File.ReadAllText(path) : "";
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			return "";
		}
	}

	private static ProcessLaunchSpec? Fail(out string? problem, string why)
	{
		problem = why;
		return null;
	}
}
