using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ActualArrow.Core.Abstractions;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Services.Install;

public static class ModManagerCompat
{
	public const string Marker = "ActualArrowLauncher";

	public const string PackageId = "speer.actualarrow";

	public const string RelativePath = "UserData/BAModManager/manifest.json";

	internal const string BackupInfix = ".bak-";

	internal const int BackupsKept = 3;

	private static string FileIn(string gameDir)
	{
		return Path.Combine(gameDir, "UserData/BAModManager/manifest.json".Replace('/', Path.DirectorySeparatorChar));
	}

	public static void Write(string gameDir, PackRecord pack, ILog log)
	{
		string text = FileIn(gameDir);
		string text2 = null;
		try
		{
			string version = pack.Manifest.Version;
			string text3 = ((version != null && version.Length > 0) ? version : (pack.Tag ?? "")).TrimStart(new char[2] { 'v', 'V' });
			bool flag = (pack.Manifest.Name ?? "").Contains("Testing", StringComparison.OrdinalIgnoreCase) || (pack.Manifest.Id ?? "").Contains("testing", StringComparison.OrdinalIgnoreCase);
			string text4 = (flag ? "ActualArrow Testing" : "ActualArrow");
			if (File.Exists(text))
			{
				string text5;
				try
				{
					text5 = File.ReadAllText(text);
				}
				catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
				{
					log.Warn("UserData/BAModManager/manifest.json could not be read: left as it is this time", ex);
					return;
				}
				if (!IsOursText(text5))
				{
					(string, bool) tuple = ArrowQueueReads(text5);
					if (tuple.Item1 == text3 && tuple.Item2 == flag)
					{
						log.Info($"{"UserData/BAModManager/manifest.json"} belongs to another tool and already says {"speer.actualarrow"} {text3}: left as it is");
						return;
					}
					string text6 = text + ".bak-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
					File.Move(text, text6, overwrite: true);
					log.Info("UserData/BAModManager/manifest.json of another tool moved aside to " + Path.GetFileName(text6) + ": ArrowQueue read " + ((tuple.Item1 == null) ? "no usable speer.actualarrow" : ("speer.actualarrow " + tuple.Item1 + (tuple.Item2 ? " (Testing)" : ""))) + " from it, the installed pack is " + text3 + (flag ? " (Testing)" : ""));
					PruneBackups(text, log);
				}
			}
			JsonArray jsonArray = new JsonArray();
			foreach (InstalledFile file in pack.Files)
			{
				jsonArray.Add(file.Path);
			}
			string text7 = new JsonObject
			{
				["writtenBy"] = "ActualArrowLauncher",
				["packages"] = new JsonArray
				{
					new JsonObject
					{
						["info"] = new JsonObject
						{
							["id"] = "speer.actualarrow",
							["name"] = text4,
							["version"] = text3
						},
						["enabled"] = true,
						["files"] = jsonArray
					}
				}
			}.ToJsonString(new JsonSerializerOptions
			{
				WriteIndented = true
			});
			if (!File.Exists(text) || !(File.ReadAllText(text) == text7))
			{
				Directory.CreateDirectory(Path.GetDirectoryName(text));
				text2 = text + ".tmp";
				File.WriteAllText(text2, text7, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				File.Move(text2, text, overwrite: true);
				text2 = null;
				log.Info($"{"UserData/BAModManager/manifest.json"} written for ArrowQueue: {"speer.actualarrow"} {text3}, {pack.Files.Count} files");
			}
		}
		catch (Exception exception)
		{
			log.Warn("Could not write UserData/BAModManager/manifest.json; ArrowQueue will show «no ActualArrow» or another version", exception);
			if (text2 != null)
			{
				try
				{
					File.Delete(text2);
					return;
				}
				catch (Exception)
				{
					return;
				}
			}
		}
	}

	public static void Remove(string gameDir, ILog log)
	{
		string text = FileIn(gameDir);
		try
		{
			if (!File.Exists(text) || !IsOurs(text))
			{
				return;
			}
			File.Delete(text);
			log.Info("UserData/BAModManager/manifest.json removed with the pack");
			string text2 = Backups(text).FirstOrDefault();
			if (text2 != null)
			{
				File.Move(text2, text);
				log.Info("UserData/BAModManager/manifest.json of another tool put back from " + Path.GetFileName(text2));
				return;
			}
			string directoryName = Path.GetDirectoryName(text);
			if (Directory.Exists(directoryName) && !Directory.EnumerateFileSystemEntries(directoryName).Any())
			{
				Directory.Delete(directoryName);
			}
		}
		catch (Exception exception)
		{
			log.Warn("Could not remove UserData/BAModManager/manifest.json", exception);
		}
	}

	private static IEnumerable<string> Backups(string path)
	{
		string directoryName = Path.GetDirectoryName(path);
		if (!Directory.Exists(directoryName))
		{
			return Array.Empty<string>();
		}
		return Directory.GetFiles(directoryName, Path.GetFileName(path) + ".bak-*").OrderByDescending((string f) => Path.GetFileName(f), StringComparer.Ordinal);
	}

	private static void PruneBackups(string path, ILog log)
	{
		foreach (string item in Backups(path).Skip(3))
		{
			try
			{
				File.Delete(item);
				log.Info($"Old {Path.GetFileName(item)} deleted (the newest {3} are kept)");
			}
			catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
			{
				log.Warn("Could not delete " + Path.GetFileName(item), ex);
			}
		}
	}

	internal static (string? Version, bool Testing) ArrowQueueReads(string text)
	{
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(text);
			string item = null;
			bool item2 = false;
			if (jsonDocument.RootElement.TryGetProperty("packages", out var value))
			{
				foreach (JsonElement item3 in value.EnumerateArray())
				{
					if (!item3.TryGetProperty("info", out var value2) || !value2.TryGetProperty("id", out var value3) || !string.Equals(value3.GetString(), "speer.actualarrow", StringComparison.OrdinalIgnoreCase) || (item3.TryGetProperty("enabled", out var value4) && value4.ValueKind == JsonValueKind.False))
					{
						continue;
					}
					item = (value2.TryGetProperty("version", out var value5) ? value5.GetString() : "?");
					item2 = value2.TryGetProperty("name", out var value6) && (value6.GetString() ?? "").IndexOf("Testing", StringComparison.OrdinalIgnoreCase) >= 0;
					if (!item3.TryGetProperty("files", out var value7))
					{
						continue;
					}
					foreach (JsonElement item4 in value7.EnumerateArray())
					{
						item4.GetString();
					}
				}
			}
			return (Version: item, Testing: item2);
		}
		catch (Exception)
		{
			return (Version: null, Testing: false);
		}
	}

	internal static (string? Version, bool Testing) VersionArrowQueueReads(string path)
	{
		try
		{
			return ArrowQueueReads(File.ReadAllText(path));
		}
		catch (Exception)
		{
			return (Version: null, Testing: false);
		}
	}

	internal static bool IsOurs(string path)
	{
		try
		{
			return IsOursText(File.ReadAllText(path));
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			return false;
		}
	}

	private static bool IsOursText(string text)
	{
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(text);
			JsonElement value;
			return jsonDocument.RootElement.ValueKind == JsonValueKind.Object && jsonDocument.RootElement.TryGetProperty("writtenBy", out value) && value.ValueKind == JsonValueKind.String && value.GetString() == "ActualArrowLauncher";
		}
		catch (JsonException)
		{
			return false;
		}
	}
}
