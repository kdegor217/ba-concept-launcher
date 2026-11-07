using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Abstractions;
using ActualArrow.Core.Models;
using ActualArrow.Core.Services.GitHub;

namespace ActualArrow.Core.Services;

public sealed class HttpDownloader : IDownloader
{
	public static readonly TimeSpan DefaultStallTimeout = TimeSpan.FromSeconds(60L);

	private const int MaxRedirects = 5;

	private const int BufferSize = 131072;

	private readonly HttpClient _http;

	private readonly ILog _log;

	public HttpDownloader(HttpClient http, ILog log)
	{
		_http = http ?? throw new ArgumentNullException("http");
		_log = log ?? NullLog.Instance;
	}

	public async Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default(CancellationToken))
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		if (string.IsNullOrWhiteSpace(request.DestinationPath))
		{
			throw new ArgumentException("DestinationPath is empty.", "request");
		}
		ct.ThrowIfCancellationRequested();
		string destination = Path.GetFullPath(request.DestinationPath);
		string part = destination + ".part";
		TimeSpan? stallTimeout = request.StallTimeout;
		TimeSpan timeSpan;
		if (stallTimeout.HasValue)
		{
			TimeSpan valueOrDefault = stallTimeout.GetValueOrDefault();
			if (valueOrDefault > TimeSpan.Zero)
			{
				timeSpan = valueOrDefault;
				goto IL_00b1;
			}
		}
		timeSpan = DefaultStallTimeout;
		goto IL_00b1;
		IL_00b1:
		TimeSpan stall = timeSpan;
		string text = null;
		if (request.ExpectedSha256 != null)
		{
			text = Sha256Hex.Normalize(request.ExpectedSha256) ?? throw new ArgumentException("ExpectedSha256 is not a SHA-256 hex string.", "request");
		}
		long? expectedSize = request.ExpectedSize;
		if (expectedSize.HasValue && expectedSize.GetValueOrDefault() < 0)
		{
			throw new ArgumentException("ExpectedSize is negative.", "request");
		}
		if (!HostPolicy.IsAllowed(request.Url))
		{
			_log.Warn("Download refused: " + HostPolicy.ForLog(request.Url) + " is not an allowed HTTPS host.");
			throw new DownloadException(DownloadFailure.HostNotAllowed, "Host not allowed: " + HostPolicy.ForLog(request.Url));
		}
		_log.Info($"Download {HostPolicy.ForLog(request.Url)} -> {destination} (expected {request.ExpectedSize?.ToString() ?? "?"} bytes, sha256 {((text == null) ? "unchecked" : "checked")}).");
		using CancellationTokenSource stallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		bool success = false;
		try
		{
			DownloadResult downloadResult = await RunAsync(request, destination, part, text, stall, stallCts, progress, ct).ConfigureAwait(continueOnCapturedContext: false);
			success = true;
			_log.Info($"Downloaded {downloadResult.Path} ({downloadResult.Size} bytes, sha256 {downloadResult.Sha256}).");
			return downloadResult;
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			_log.Info("Download of " + Path.GetFileName(destination) + " cancelled.");
			throw;
		}
		catch (Exception ex2) when (((Func<bool>)delegate
		{
			// Could not convert BlockContainer to single expression
			bool flag = stallCts.IsCancellationRequested;
			if (flag)
			{
				flag = ((ex2 is OperationCanceledException || ex2 is IOException || ex2 is HttpRequestException) ? true : false);
			}
			return flag;
		}).Invoke())
		{
			_log.Warn($"Download of {Path.GetFileName(destination)} stalled for {stall.TotalSeconds:0} s.");
			throw new DownloadException(DownloadFailure.Timeout, $"No data for {stall.TotalSeconds:0} s.", null, ex2);
		}
		catch (DownloadException ex3)
		{
			_log.Warn($"Download of {Path.GetFileName(destination)} failed: {ex3.Failure}: {ex3.Message}");
			throw;
		}
		catch (HttpRequestException ex4)
		{
			_log.Warn($"Download of {Path.GetFileName(destination)}: network failure ({ex4.HttpRequestError}).", ex4);
			string message = $"{ex4.HttpRequestError}: {ex4.Message}";
			Exception inner = ex4;
			throw new DownloadException(DownloadFailure.Network, message, null, inner);
		}
		catch (IOException ex5)
		{
			_log.Warn("Download of " + Path.GetFileName(destination) + ": connection broke.", ex5);
			string message2 = "Connection broke: " + ex5.Message;
			Exception inner = ex5;
			throw new DownloadException(DownloadFailure.Network, message2, null, inner);
		}
		catch (OperationCanceledException ex6)
		{
			_log.Warn("Download of " + Path.GetFileName(destination) + " was aborted.", ex6);
			string message3 = "Aborted: " + ex6.Message;
			Exception inner = ex6;
			throw new DownloadException(DownloadFailure.Network, message3, null, inner);
		}
		finally
		{
			if (!success)
			{
				TryDelete(part);
			}
		}
	}

	private async Task<DownloadResult> RunAsync(DownloadRequest request, string destination, string part, string? expectedHash, TimeSpan stall, CancellationTokenSource stallCts, IProgress<DownloadProgress>? progress, CancellationToken userToken)
	{
		CancellationToken token = stallCts.Token;
		stallCts.CancelAfter(stall);
		using HttpResponseMessage response = await SendFollowingRedirectsAsync(request.Url, token).ConfigureAwait(continueOnCapturedContext: false);
		if (!response.IsSuccessStatusCode)
		{
			int statusCode = (int)response.StatusCode;
			throw new DownloadException(DownloadFailure.HttpStatus, $"HTTP {statusCode} {response.ReasonPhrase}", statusCode);
		}
		long? contentLength = response.Content.Headers.ContentLength;
		long? expected = request.ExpectedSize;
		if (expected.HasValue && contentLength.HasValue && contentLength != expected)
		{
			throw new DownloadException(DownloadFailure.SizeMismatch, $"Server announces {contentLength} bytes, expected {expected}.");
		}
		long? total = expected ?? contentLength;
		CreateParentFolder(destination);
		TryDelete(part);
		using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		long done = 0L;
		progress?.Report(new DownloadProgress(0L, total));
		await using (Stream source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(continueOnCapturedContext: false))
		{
			await using FileStream target = OpenPart(part);
			byte[] buffer = new byte[131072];
			while (true)
			{
				stallCts.CancelAfter(stall);
				int read = await source.ReadAsync(buffer, token).ConfigureAwait(continueOnCapturedContext: false);
				if (read == 0)
				{
					break;
				}
				if (expected.HasValue && done + read > expected)
				{
					throw new DownloadException(DownloadFailure.SizeMismatch, $"Received more than the expected {expected} bytes.");
				}
				hash.AppendData(buffer, 0, read);
				await WriteAsync(target, buffer, read, userToken).ConfigureAwait(continueOnCapturedContext: false);
				done += read;
				progress?.Report(new DownloadProgress(done, total));
			}
			stallCts.CancelAfter(Timeout.InfiniteTimeSpan);
			await FlushAsync(target, userToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (expected.HasValue && done != expected)
		{
			throw new DownloadException(DownloadFailure.SizeMismatch, $"Received {done} bytes, expected {expected}.");
		}
		if (!expected.HasValue && contentLength.HasValue && done != contentLength)
		{
			throw new DownloadException(DownloadFailure.SizeMismatch, $"Received {done} bytes, server announced {contentLength}.");
		}
		string text = Convert.ToHexStringLower(hash.GetHashAndReset());
		if (expectedHash != null && !string.Equals(text, expectedHash, StringComparison.Ordinal))
		{
			throw new DownloadException(DownloadFailure.HashMismatch, $"SHA-256 {text}, expected {expectedHash}.");
		}
		userToken.ThrowIfCancellationRequested();
		try
		{
			File.Move(part, destination, overwrite: true);
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			throw new DownloadException(DownloadFailure.Io, "Could not move the downloaded file into place: " + ex.Message, null, ex);
		}
		return new DownloadResult(destination, done, text);
	}

	private async Task<HttpResponseMessage> SendFollowingRedirectsAsync(Uri url, CancellationToken token)
	{
		Uri current = url;
		int hop = 0;
		while (true)
		{
			using (HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, current))
			{
				message.Headers.TryAddWithoutValidation("User-Agent", Brand.UserAgent);
				message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
				HttpResponseMessage httpResponseMessage = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(continueOnCapturedContext: false);
				try
				{
					Uri uri = httpResponseMessage.RequestMessage?.RequestUri ?? current;
					if (!HostPolicy.IsAllowed(uri))
					{
						throw new DownloadException(DownloadFailure.HostNotAllowed, "Redirected to a host that is not allowed: " + HostPolicy.ForLog(uri));
					}
					if (!IsRedirect(httpResponseMessage.StatusCode))
					{
						return httpResponseMessage;
					}
					Uri location = httpResponseMessage.Headers.Location;
					if ((object)location == null)
					{
						throw new DownloadException(DownloadFailure.HttpStatus, $"HTTP {(int)httpResponseMessage.StatusCode} without Location.", (int)httpResponseMessage.StatusCode);
					}
					if (hop >= 5)
					{
						throw new DownloadException(DownloadFailure.HttpStatus, $"More than {5} redirects.", (int)httpResponseMessage.StatusCode);
					}
					Uri uri2 = (location.IsAbsoluteUri ? location : new Uri(uri, location));
					if (!HostPolicy.IsAllowed(uri2))
					{
						throw new DownloadException(DownloadFailure.HostNotAllowed, "Redirect to a host that is not allowed: " + HostPolicy.ForLog(uri2));
					}
					_log.Debug("Download redirect -> " + HostPolicy.ForLog(uri2));
					current = uri2;
					httpResponseMessage.Dispose();
				}
				catch
				{
					httpResponseMessage.Dispose();
					throw;
				}
			}
			hop++;
		}
	}

	private static bool IsRedirect(HttpStatusCode code)
	{
		if ((uint)(code - 301) <= 2u || (uint)(code - 307) <= 1u)
		{
			return true;
		}
		return false;
	}

	private static void CreateParentFolder(string destination)
	{
		try
		{
			string directoryName = Path.GetDirectoryName(destination);
			if (!string.IsNullOrEmpty(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			throw new DownloadException(DownloadFailure.Io, "Could not create the download folder: " + ex.Message, null, ex);
		}
	}

	private static FileStream OpenPart(string part)
	{
		try
		{
			return new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 131072, FileOptions.Asynchronous);
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			throw new DownloadException(DownloadFailure.Io, "Could not create the .part file: " + ex.Message, null, ex);
		}
	}

	private static async Task WriteAsync(FileStream target, byte[] buffer, int count, CancellationToken ct)
	{
		try
		{
			await target.WriteAsync(buffer.AsMemory(0, count), ct).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			throw new DownloadException(DownloadFailure.Io, "Could not write the .part file: " + ex.Message, null, ex);
		}
	}

	private static async Task FlushAsync(FileStream target, CancellationToken ct)
	{
		try
		{
			await target.FlushAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			throw new DownloadException(DownloadFailure.Io, "Could not write the .part file: " + ex.Message, null, ex);
		}
	}

	private void TryDelete(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			_log.Warn("Could not delete " + path + ".", ex);
		}
	}
}
