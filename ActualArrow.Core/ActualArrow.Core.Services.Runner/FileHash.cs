using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ActualArrow.Core.Services.Runner;

internal static class FileHash
{
	public static async Task<string> Sha256Async(string path, CancellationToken ct)
	{
		string result;
		await using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true))
		{
			result = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(continueOnCapturedContext: false));
		}
		return result;
	}

	public static async Task<bool> MatchesAsync(string path, long? size, string sha256, CancellationToken ct)
	{
		try
		{
			FileInfo fileInfo = new FileInfo(path);
			if (!fileInfo.Exists)
			{
				return false;
			}
			if (size.HasValue && fileInfo.Length != size.Value)
			{
				return false;
			}
			return string.Equals(await Sha256Async(path, ct).ConfigureAwait(continueOnCapturedContext: false), sha256, StringComparison.OrdinalIgnoreCase);
		}
		catch (IOException)
		{
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}
	}
}
