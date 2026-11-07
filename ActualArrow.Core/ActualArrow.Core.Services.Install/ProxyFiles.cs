using System.Collections.Generic;

namespace ActualArrow.Core.Services.Install;

internal static class ProxyFiles
{
	public static IReadOnlyList<(string Name, string Path)> Find(string dir)
	{
		List<(string, string)> list = new List<(string, string)>();
		foreach (string proxyDllName in Brand.Game.ProxyDllNames)
		{
			string text = PackPaths.FindChild(dir, proxyDllName, directory: false);
			if (text != null)
			{
				list.Add((proxyDllName, text));
			}
		}
		return list;
	}
}
