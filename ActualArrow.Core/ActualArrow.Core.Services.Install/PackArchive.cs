using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using ActualArrow.Core.Abstractions;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Services.Install;

internal sealed class PackArchive : IDisposable
{
	private const long MaxManifestBytes = 262144L;

	private const long MaxPreviewBytes = 16777216L;

	private readonly ZipArchive _zip;

	public PackInspection Inspection { get; }

	public IReadOnlyList<(PackEntry Entry, ZipArchiveEntry Zip)> Files { get; }

	public byte[]? ManifestBytes { get; }

	public ZipArchiveEntry? PreviewEntry { get; }

	private PackArchive(ZipArchive zip, PackInspection inspection, IReadOnlyList<(PackEntry Entry, ZipArchiveEntry Zip)> files, byte[]? manifestBytes, ZipArchiveEntry? preview)
	{
		_zip = zip;
		Inspection = inspection;
		Files = files;
		ManifestBytes = manifestBytes;
		PreviewEntry = preview;
	}

	public static PackArchive Open(string packPath, ILog log)
	{
		ZipArchive zipArchive;
		try
		{
			zipArchive = ZipFile.OpenRead(packPath);
		}
		catch (Exception ex) when ((ex is InvalidDataException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException) ? true : false)
		{
			throw new InstallException(InstallFailure.InvalidPack, "Cannot open pack '" + packPath + "': " + ex.Message, ex);
		}
		try
		{
			return Classify(zipArchive, packPath, log);
		}
		catch (Exception ex2) when ((ex2 is InvalidDataException || ex2 is IOException) ? true : false)
		{
			zipArchive.Dispose();
			throw new InstallException(InstallFailure.InvalidPack, "Cannot read pack '" + packPath + "': " + ex2.Message, ex2);
		}
		catch
		{
			zipArchive.Dispose();
			throw;
		}
	}

	private static PackArchive Classify(ZipArchive zip, string packPath, ILog log)
	{
		List<(PackEntry, ZipArchiveEntry)> list = new List<(PackEntry, ZipArchiveEntry)>();
		List<SkippedEntry> list2 = new List<SkippedEntry>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		byte[] array = null;
		ZipArchiveEntry zipArchiveEntry = null;
		foreach (ZipArchiveEntry entry in zip.Entries)
		{
			EntryVerdict entryVerdict = PackPaths.ClassifyPackEntry(entry.FullName);
			switch (entryVerdict.Kind)
			{
			case EntryKind.Manifest:
				if (array == null)
				{
					if (entry.Length <= 262144)
					{
						array = ReadAll(entry);
					}
					else
					{
						log.Warn("Pack " + Path.GetFileName(packPath) + ": mod.json is too large, ignored");
					}
					break;
				}
				goto IL_00f4;
			case EntryKind.Preview:
				if (zipArchiveEntry == null)
				{
					if (entry.Length <= 16777216)
					{
						zipArchiveEntry = entry;
					}
					else
					{
						log.Warn("Pack " + Path.GetFileName(packPath) + ": preview.png is too large, ignored");
					}
					break;
				}
				goto IL_00f4;
			case EntryKind.Rejected:
				list2.Add(new SkippedEntry(entry.FullName, entryVerdict.Reason ?? "rejected"));
				break;
			case EntryKind.PackFile:
				{
					if (!hashSet.Add(entryVerdict.RelativePath))
					{
						list2.Add(new SkippedEntry(entry.FullName, "duplicate entry"));
					}
					else
					{
						list.Add((new PackEntry(entryVerdict.RelativePath, entry.FullName, entry.Length), entry));
					}
					break;
				}
				IL_00f4:
				list2.Add(new SkippedEntry(entry.FullName, "duplicate entry"));
				break;
			}
		}
		HashSet<string> asFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var item in list)
		{
			string relativePath = item.Item1.RelativePath;
			for (int num = relativePath.IndexOf('/'); num >= 0; num = relativePath.IndexOf('/', num + 1))
			{
				asFolders.Add(relativePath.Substring(0, num));
			}
		}
		foreach (var item2 in list.Where(((PackEntry, ZipArchiveEntry) f) => asFolders.Contains(f.Item1.RelativePath)).ToList())
		{
			list.Remove(item2);
			list2.Add(new SkippedEntry(item2.Item1.EntryName, "file conflicts with a folder"));
		}
		foreach (SkippedEntry item3 in list2)
		{
			log.Info($"Pack entry skipped: '{item3.EntryName}' ({item3.Reason})");
		}
		PackManifest manifest = ((array == null) ? PackManifest.Empty : ParseManifest(array, log));
		PackInspection inspection = new PackInspection(packPath, manifest, list.Select(((PackEntry, ZipArchiveEntry) f) => f.Item1).ToList(), list2, zipArchiveEntry != null);
		return new PackArchive(zip, inspection, list, array, zipArchiveEntry);
	}

	public static PackManifest ParseManifest(byte[] bytes, ILog log)
	{
		try
		{
			JsonDocument doc = JsonDocument.Parse(bytes, new JsonDocumentOptions
			{
				AllowTrailingCommas = true,
				CommentHandling = JsonCommentHandling.Skip
			});
			try
			{
				if (doc.RootElement.ValueKind != JsonValueKind.Object)
				{
					return PackManifest.Empty;
				}
				return new PackManifest
				{
					Id = Get("id"),
					Name = Get("name"),
					Version = Get("version"),
					Author = Get("author"),
					Description = Get("description"),
					GameVersion = Get("gameVersion")
				};
			}
			finally
			{
				if (doc != null)
				{
					((IDisposable)doc).Dispose();
				}
			}
			string? Get(string name)
			{
				foreach (JsonProperty item in doc.RootElement.EnumerateObject())
				{
					if (item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
					{
						switch (item.Value.ValueKind)
						{
						case JsonValueKind.String:
							return item.Value.GetString();
						case JsonValueKind.Number:
						case JsonValueKind.True:
						case JsonValueKind.False:
							return item.Value.GetRawText();
						default:
							return null;
						}
					}
				}
				return null;
			}
		}
		catch (JsonException ex)
		{
			log.Warn("mod.json is not valid JSON: " + ex.Message);
			return PackManifest.Empty;
		}
	}

	public byte[]? ReadPreview()
	{
		if (PreviewEntry != null)
		{
			return ReadAll(PreviewEntry);
		}
		return null;
	}

	private static byte[] ReadAll(ZipArchiveEntry entry)
	{
		using Stream stream = entry.Open();
		byte[] array = new byte[entry.Length];
		int i;
		int num;
		for (i = 0; i < array.Length; i += num)
		{
			num = stream.Read(array, i, array.Length - i);
			if (num == 0)
			{
				break;
			}
		}
		if (i != array.Length || stream.ReadByte() != -1)
		{
			throw new InvalidDataException("Entry '" + entry.FullName + "' does not match its declared size.");
		}
		return array;
	}

	public void Dispose()
	{
		_zip.Dispose();
	}
}
