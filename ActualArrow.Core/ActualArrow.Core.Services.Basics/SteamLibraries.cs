using System.Collections.Generic;
using System.IO;
using System.Linq;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Services.Basics;

internal static class SteamLibraries
{
	public static IReadOnlyList<SteamLibrary> FromLibraryFolders(VdfNode document, string appId)
	{
		VdfNode vdfNode = document["libraryfolders"] ?? FirstBlock(document);
		List<SteamLibrary> list = new List<SteamLibrary>();
		if (vdfNode == null)
		{
			return list;
		}
		foreach (VdfNode child in vdfNode.Children)
		{
			if (!IsIndexKey(child.Key))
			{
				continue;
			}
			if (child.IsBlock)
			{
				string value = child.GetValue("path");
				if (!string.IsNullOrWhiteSpace(value))
				{
					VdfNode vdfNode2 = child["apps"];
					bool listsGame = vdfNode2 != null && vdfNode2.IsBlock && vdfNode2[appId] != null;
					list.Add(new SteamLibrary(value.Trim(), listsGame));
				}
			}
			else if (!string.IsNullOrWhiteSpace(child.Value))
			{
				list.Add(new SteamLibrary(child.Value.Trim(), ListsGame: false));
			}
		}
		return list;
	}

	public static string? InstallDirFromManifest(VdfNode document)
	{
		string text = (document["AppState"] ?? FirstBlock(document))?.GetValue("installdir")?.Trim();
		if (!IsPlainFolderName(text))
		{
			return null;
		}
		return text;
	}

	public static bool IsPlainFolderName(string? name)
	{
		if (!string.IsNullOrWhiteSpace(name) && name.IndexOfAny(new char[4] { '/', '\\', ':', '\0' }) < 0 && name.Trim('.').Length > 0)
		{
			return name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
		}
		return false;
	}

	private static VdfNode? FirstBlock(VdfNode document)
	{
		return document.Children.FirstOrDefault((VdfNode c) => c.IsBlock);
	}

	private static bool IsIndexKey(string key)
	{
		if (key.Length == 0)
		{
			return false;
		}
		foreach (char c in key)
		{
			if ((c < '0' || c > '9') ? true : false)
			{
				return false;
			}
		}
		return true;
	}
}
