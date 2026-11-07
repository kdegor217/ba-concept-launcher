using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ActualArrow.Core.Abstractions;

namespace ActualArrow.Core.Services.Runner;

internal static class LoaderScan
{
	private const int ChunkSize = 1048576;

	private const long MaxBytesRead = 67108864L;

	private static readonly byte[][] Signatures = new byte[6][]
	{
		Encoding.ASCII.GetBytes("MelonLoader"),
		Encoding.Unicode.GetBytes("MelonLoader"),
		Encoding.ASCII.GetBytes("doorstop"),
		Encoding.ASCII.GetBytes("Doorstop"),
		Encoding.Unicode.GetBytes("Doorstop"),
		Encoding.ASCII.GetBytes("BepInEx")
	};

	public static IReadOnlyList<string> FindOtherLoaders(string gameDir, ILog log)
	{
		if (!Directory.Exists(gameDir))
		{
			return Array.Empty<string>();
		}
		List<string> list = new List<string>();
		IEnumerable<string> enumerable;
		try
		{
			enumerable = Directory.EnumerateFiles(gameDir).ToList();
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			log.Warn("Could not list " + gameDir, ex);
			return Array.Empty<string>();
		}
		foreach (string item in enumerable)
		{
			string name = Path.GetFileName(item);
			if (Brand.Game.OtherLoaderProxyNames.Any((string n) => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) && LooksLikeLoader(item, log))
			{
				log.Warn("Mod loader found under another name in the game root: " + name);
				list.Add(name);
			}
		}
		return list;
	}

	internal static bool LooksLikeLoader(string path, ILog log)
	{
		try
		{
			using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			int num = Signatures.Max((byte[] s) => s.Length) - 1;
			byte[] array = new byte[1048576 + num];
			int num2 = 0;
			long num3 = 0L;
			while (num3 < 67108864)
			{
				int num4 = fileStream.Read(array, num2, 1048576);
				if (num4 <= 0)
				{
					break;
				}
				num3 += num4;
				Span<byte> span = array.AsSpan(0, num2 + num4);
				byte[][] signatures = Signatures;
				foreach (byte[] array2 in signatures)
				{
					if (((ReadOnlySpan<byte>)span).IndexOf((ReadOnlySpan<byte>)array2) >= 0)
					{
						return true;
					}
				}
				num2 = Math.Min(num, span.Length);
				int num5 = num2;
				int length = span.Length;
				int num6 = length - num5;
				span.Slice(num6, length - num6).CopyTo(array);
			}
			return false;
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			log.Warn("Could not read " + path + "; treating it as a mod loader", ex);
			return true;
		}
	}
}
