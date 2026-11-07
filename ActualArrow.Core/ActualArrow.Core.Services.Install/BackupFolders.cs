using System;
using System.CodeDom.Compiler;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions.Generated;
using ActualArrow.Core.Abstractions;

namespace ActualArrow.Core.Services.Install;

internal sealed class BackupFolders
{
	private readonly string _root;

	private readonly TimeProvider _time;

	private readonly ILog _log;

	public const string KeepMarker = "KEEP.txt";

	public string Root => _root;

	public BackupFolders(string root, TimeProvider time, ILog log)
	{
		_root = root;
		_time = time;
		_log = log;
	}

	public string CreateSession()
	{
		Directory.CreateDirectory(_root);
		string text = _time.GetUtcNow().UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
		int num = 1;
		string text2;
		while (true)
		{
			string path = ((num == 1) ? text : $"{text}-{num}");
			text2 = Path.Combine(_root, path);
			if (!Directory.Exists(text2) && !File.Exists(text2))
			{
				break;
			}
			num++;
		}
		Directory.CreateDirectory(text2);
		return text2;
	}

	public void MarkKept(string sessionDir, string why)
	{
		try
		{
			File.WriteAllText(Path.Combine(sessionDir, "KEEP.txt"), "Kept by the launcher: " + why + ".\nIt is not deleted automatically; delete this folder yourself when you no longer need it.\n");
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			_log.Warn("Could not mark backup " + sessionDir + " as kept", ex);
		}
	}

	public void Prune(int keep = 3)
	{
		if (!Directory.Exists(_root))
		{
			return;
		}
		foreach (var item3 in (from p in Directory.EnumerateDirectories(_root)
			select (Path: p, Key: ParseKey(Path.GetFileName(p)))).Where(((string Path, (string Stamp, int N)? Key) x) =>
		{
			(string, int)? item2 = x.Key;
			return item2.HasValue && !File.Exists(Path.Combine(x.Path, "KEEP.txt"));
		}).OrderByDescending(((string Path, (string Stamp, int N)? Key) x) => x.Key.Value.Stamp, StringComparer.Ordinal).ThenByDescending(((string Path, (string Stamp, int N)? Key) x) => x.Key.Value.N)
			.Skip(Math.Max(keep, 0))
			.ToList())
		{
			string item = item3.Path;
			if (FsOps.TryDeleteDirectory(item))
			{
				_log.Info("Old backup removed: " + Path.GetFileName(item));
			}
			else
			{
				_log.Warn("Could not remove old backup " + item);
			}
		}
	}

	private static (string Stamp, int N)? ParseKey(string name)
	{
		Match match = SessionName().Match(name);
		if (!match.Success)
		{
			return null;
		}
		int item = ((!match.Groups[2].Success) ? 1 : int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
		return (match.Groups[1].Value, item);
	}

	private static Regex SessionName()
	{
		return _003CRegexGenerator_g_003EFC2F671FD3A111B0AC8B64BAC66ABD7A103065EAF2FDEC5695C23E559BA17C004__SessionName_0.Instance;
	}
}
