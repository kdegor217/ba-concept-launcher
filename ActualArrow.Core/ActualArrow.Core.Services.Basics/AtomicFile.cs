using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ActualArrow.Core.Services.Basics;

public static class AtomicFile
{
	private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	public static Task WriteAllTextAsync(string path, string text, CancellationToken ct = default(CancellationToken))
	{
		return WriteAllBytesAsync(path, Utf8NoBom.GetBytes(text), ct);
	}

	public static async Task WriteAllBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken ct = default(CancellationToken))
	{
		ArgumentException.ThrowIfNullOrEmpty(path, "path");
		string full = Path.GetFullPath(path);
		string directoryName = Path.GetDirectoryName(full);
		Directory.CreateDirectory(directoryName);
		string temp = Path.Combine(directoryName, $".{Path.GetFileName(full)}.{Guid.NewGuid():N}.tmp");
		try
		{
			await using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
			{
				await stream.WriteAsync(bytes, ct).ConfigureAwait(continueOnCapturedContext: false);
				await stream.FlushAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
				stream.Flush(flushToDisk: true);
			}
			ct.ThrowIfCancellationRequested();
			File.Move(temp, full, overwrite: true);
		}
		finally
		{
			TryDelete(temp);
		}
	}

	private static void TryDelete(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch (IOException)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}
	}
}
