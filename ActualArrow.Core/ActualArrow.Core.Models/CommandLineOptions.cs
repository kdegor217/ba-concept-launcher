using System;
using System.Collections.Generic;
using System.Globalization;

namespace ActualArrow.Core.Models;

public sealed record CommandLineOptions
{
	public string? GameDir { get; init; }

	public string? Repo { get; init; }

	public string? AppData { get; init; }

	public bool DryRun { get; init; }

	public bool WatchGame { get; init; }

	public int? AfterPid { get; init; }

	public IReadOnlyList<string> Unknown { get; init; } = Array.Empty<string>();

	public static CommandLineOptions Empty { get; } = new CommandLineOptions();

	public static CommandLineOptions Parse(IReadOnlyList<string> args)
	{
		string gameDir = null;
		string repo = null;
		string appData = null;
		bool dryRun = false;
		bool watchGame = false;
		int? afterPid = null;
		List<string> list = new List<string>();
		for (int i = 0; i < args.Count; i++)
		{
			string text = args[i];
			string text2 = text;
			string text3 = null;
			int num = text.IndexOf('=');
			if (text.StartsWith("--", StringComparison.Ordinal) && num > 2)
			{
				text2 = text.Substring(0, num);
				string text4 = text;
				int num2 = num + 1;
				text3 = text4.Substring(num2, text4.Length - num2);
			}
			switch (text2.ToLowerInvariant())
			{
			case "--dry-run":
				dryRun = true;
				break;
			case "--watch-game":
				watchGame = true;
				break;
			case "--after-pid":
			{
				if (int.TryParse(text3 ?? ((i + 1 < args.Count) ? args[++i] : null), NumberStyles.None, CultureInfo.InvariantCulture, out var result) && result > 0)
				{
					afterPid = result;
				}
				else
				{
					list.Add(text);
				}
				break;
			}
			case "--game-dir":
			case "--repo":
			case "--appdata":
			{
				string text5 = text3;
				if (text5 == null && i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
				{
					text5 = args[++i];
				}
				if (string.IsNullOrWhiteSpace(text5))
				{
					list.Add(text);
				}
				else if (text2.Equals("--game-dir", StringComparison.OrdinalIgnoreCase))
				{
					gameDir = text5;
				}
				else if (text2.Equals("--repo", StringComparison.OrdinalIgnoreCase))
				{
					repo = text5;
				}
				else
				{
					appData = text5;
				}
				break;
			}
			default:
				list.Add(text);
				break;
			}
		}
		return new CommandLineOptions
		{
			GameDir = gameDir,
			Repo = repo,
			AppData = appData,
			DryRun = dryRun,
			WatchGame = watchGame,
			AfterPid = afterPid,
			Unknown = list
		};
	}
}
