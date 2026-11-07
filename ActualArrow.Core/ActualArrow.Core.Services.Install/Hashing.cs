using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace ActualArrow.Core.Services.Install;

internal static class Hashing
{
	private const int BufferSize = 131072;

	public static string FileSha256(string path, CancellationToken ct = default(CancellationToken))
	{
		using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.SequentialScan);
		using IncrementalHash incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		byte[] array = new byte[131072];
		int count;
		while ((count = fileStream.Read(array, 0, array.Length)) > 0)
		{
			ct.ThrowIfCancellationRequested();
			incrementalHash.AppendData(array, 0, count);
		}
		return Convert.ToHexStringLower(incrementalHash.GetHashAndReset());
	}

	public static (long Size, string Sha256) CopyToNewFile(Stream source, string destination, long expectedLength, CancellationToken ct)
	{
		try
		{
			using FileStream fileStream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072);
			using IncrementalHash incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
			byte[] array = new byte[131072];
			long num = 0L;
			int num2;
			while ((num2 = source.Read(array, 0, array.Length)) > 0)
			{
				ct.ThrowIfCancellationRequested();
				num += num2;
				if (num > expectedLength)
				{
					throw new InvalidDataException($"Entry is larger than its declared size ({expectedLength} bytes).");
				}
				incrementalHash.AppendData(array, 0, num2);
				fileStream.Write(array, 0, num2);
			}
			if (num != expectedLength)
			{
				throw new InvalidDataException($"Entry is {num} bytes, declared {expectedLength}.");
			}
			return (Size: num, Sha256: Convert.ToHexStringLower(incrementalHash.GetHashAndReset()));
		}
		catch
		{
			FsOps.TryDeleteFile(destination);
			throw;
		}
	}

	public static bool SameHash(string? a, string? b)
	{
		if (a != null && b != null)
		{
			return string.Equals(Strip(a), Strip(b), StringComparison.OrdinalIgnoreCase);
		}
		return false;
		static string Strip(string s)
		{
			if (!s.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
			{
				return s;
			}
			return s.Substring(7, s.Length - 7);
		}
	}
}
