using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Abstractions;
using ActualArrow.Core.Models;
using ActualArrow.Core.Services.GitHub;

namespace ActualArrow.Core.Services;

public sealed class GitHubReleaseFeed : IReleaseFeed
{
	private sealed record CachedRelease(string Etag, string Json);

	private const int MaxBodyBytes = 4194304;

	private static readonly Regex RepoRegex = new Regex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})/[A-Za-z0-9._-]{1,100}$", RegexOptions.CultureInvariant);

	private readonly HttpClient _http;

	private readonly ISettingsStore _settings;

	private readonly ILog _log;

	private readonly TimeProvider _time;

	private readonly TimeSpan _checkTimeout;

	public GitHubReleaseFeed(HttpClient http, ISettingsStore settings, ILog log, TimeProvider? time = null, TimeSpan? checkTimeout = null)
	{
		_http = http ?? throw new ArgumentNullException("http");
		_settings = settings ?? throw new ArgumentNullException("settings");
		_log = log ?? NullLog.Instance;
		_time = time ?? TimeProvider.System;
		TimeSpan checkTimeout2;
		if (checkTimeout.HasValue)
		{
			TimeSpan valueOrDefault = checkTimeout.GetValueOrDefault();
			if (valueOrDefault > TimeSpan.Zero)
			{
				checkTimeout2 = valueOrDefault;
				goto IL_007a;
			}
		}
		checkTimeout2 = Brand.GitHub.CheckTimeout;
		goto IL_007a;
		IL_007a:
		_checkTimeout = checkTimeout2;
	}

	public async Task<ReleaseFeedResult> GetLatestAsync(string repo, CancellationToken ct = default(CancellationToken))
	{
		repo = (repo ?? string.Empty).Trim();
		if (!RepoRegex.IsMatch(repo) || repo.Contains(".."))
		{
			_log.Warn("Release check skipped: «" + repo + "» is not owner/name.");
			return ReleaseFeedResult.Fail(new ReleaseFeedError.Other(null, "invalid repo: " + repo));
		}
		ct.ThrowIfCancellationRequested();
		using CancellationTokenSource timeout = new CancellationTokenSource(_checkTimeout, _time);
		using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
		_ = 1;
		try
		{
			CachedRelease cache = UsableCache(_settings.Current, repo);
			(ReleaseFeedResult, bool) tuple = await CheckAsync(repo, cache, linked.Token, ct).ConfigureAwait(continueOnCapturedContext: false);
			if (tuple.Item2)
			{
				_log.Warn("Cached release JSON is unusable after 304; asking again without ETag.");
				tuple = await CheckAsync(repo, null, linked.Token, ct).ConfigureAwait(continueOnCapturedContext: false);
			}
			return tuple.Item1;
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (OperationCanceledException) when (timeout.IsCancellationRequested)
		{
			_log.Warn($"Release check for {repo} timed out after {_checkTimeout.TotalSeconds:0} s.");
			return ReleaseFeedResult.Fail(new ReleaseFeedError.Offline($"timeout after {_checkTimeout.TotalSeconds:0} s"));
		}
		catch (OperationCanceledException ex3)
		{
			_log.Warn("Release check for " + repo + " was aborted.", ex3);
			return ReleaseFeedResult.Fail(new ReleaseFeedError.Offline("aborted: " + ex3.Message));
		}
		catch (HttpRequestException ex4)
		{
			_log.Warn($"Release check for {repo}: network failure ({ex4.HttpRequestError}).", ex4);
			return ReleaseFeedResult.Fail(new ReleaseFeedError.Offline($"{ex4.HttpRequestError}: {ex4.Message}"));
		}
		catch (IOException ex5)
		{
			_log.Warn("Release check for " + repo + ": connection broke while reading.", ex5);
			return ReleaseFeedResult.Fail(new ReleaseFeedError.Offline("io: " + ex5.Message));
		}
		catch (Exception ex6)
		{
			_log.Error("Release check for " + repo + " failed unexpectedly.", ex6);
			return ReleaseFeedResult.Fail(new ReleaseFeedError.Other(null, ex6.GetType().Name + ": " + ex6.Message));
		}
	}

	private async Task<(ReleaseFeedResult? Value, bool Retry)> CheckAsync(string repo, CachedRelease? cache, CancellationToken token, CancellationToken userToken)
	{
		Uri url = new Uri(new Uri("https://api.github.com/"), Brand.GitHub.LatestReleasePath(repo));
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
		request.Headers.TryAddWithoutValidation("User-Agent", Brand.UserAgent);
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
		if ((object)cache != null)
		{
			request.Headers.TryAddWithoutValidation("If-None-Match", cache.Etag);
		}
		using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(continueOnCapturedContext: false);
		int status = (int)response.StatusCode;
		Uri uri = response.RequestMessage?.RequestUri ?? url;
		if (!HostPolicy.IsApiHost(uri))
		{
			_log.Warn($"Release check for {repo} ended on a foreign host {HostPolicy.ForLog(uri)}.");
			return (Value: ReleaseFeedResult.Fail(new ReleaseFeedError.Other(status, "foreign host: " + uri.Host)), Retry: false);
		}
		if (response.StatusCode == HttpStatusCode.NotModified)
		{
			if ((object)cache == null)
			{
				return (Value: ReleaseFeedResult.Fail(new ReleaseFeedError.Other(status, "304 without a cached release")), Retry: false);
			}
			ParsedRelease cached = ReleaseJson.Parse(repo, cache.Json);
			if (!cached.IsReleaseDocument)
			{
				return (Value: null, Retry: true);
			}
			await SaveAsync((LauncherSettings s) => s with
			{
				LastCheckUtc = _time.GetUtcNow()
			}, userToken).ConfigureAwait(continueOnCapturedContext: false);
			if ((object)cached.Release == null)
			{
				_log.Warn("Release check for " + repo + ": 304, cached release has no pack asset.");
				return (Value: ReleaseFeedResult.Fail(cached.Error), Retry: false);
			}
			_log.Info($"Release check for {repo}: 304, cached {cached.Release.Tag} ({cached.Release.Asset.Name}).");
			return (Value: ReleaseFeedResult.Ok(cached.Release, fromCache: true), Retry: false);
		}
		if (response.StatusCode == HttpStatusCode.OK)
		{
			string body = await ReadBodyAsync(response, token).ConfigureAwait(continueOnCapturedContext: false);
			if (body == null)
			{
				return (Value: ReleaseFeedResult.Fail(new ReleaseFeedError.Other(status, $"body larger than {4194304} bytes")), Retry: false);
			}
			ParsedRelease cached = ReleaseJson.Parse(repo, body);
			if (!cached.IsReleaseDocument)
			{
				_log.Warn($"Release check for {repo}: 200 with an unusable body ({cached.Error?.Detail}).");
				return (Value: ReleaseFeedResult.Fail(new ReleaseFeedError.Other(status, cached.Error?.Detail)), Retry: false);
			}
			string etag = ReadEtag(response);
			await SaveAsync((LauncherSettings s) => s with
			{
				LastEtag = etag,
				LastReleaseJson = body,
				LastReleaseRepo = repo,
				LastCheckUtc = _time.GetUtcNow()
			}, userToken).ConfigureAwait(continueOnCapturedContext: false);
			if ((object)cached.Release == null)
			{
				_log.Warn($"Release check for {repo}: {cached.Error.Detail}.");
				return (Value: ReleaseFeedResult.Fail(cached.Error), Retry: false);
			}
			ReleaseAsset asset = cached.Release.Asset;
			_log.Info($"Release check for {repo}: 200, {cached.Release.Tag}, asset {asset.Name} ({asset.Size} bytes, digest {((asset.Sha256 == null) ? "absent" : "present")}).");
			return (Value: ReleaseFeedResult.Ok(cached.Release), Retry: false);
		}
		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			_log.Warn("Release check for " + repo + ": 404 (no published release, or the repo is missing/private).");
			return (Value: ReleaseFeedResult.Fail(new ReleaseFeedError.NotFound("404 for " + repo)), Retry: false);
		}
		if (RateLimitHeaders.IsRateLimited(response))
		{
			DateTimeOffset? resetUtc = RateLimitHeaders.ResetTime(response, _time.GetUtcNow());
			_log.Warn($"Release check for {repo}: rate limited ({status}), resets {((!resetUtc.HasValue) ? "unknown" : resetUtc.Value.ToString("u"))}.");
			return (Value: ReleaseFeedResult.Fail(new ReleaseFeedError.RateLimited(resetUtc, $"{status}, remaining {RateLimitHeaders.Remaining(response.Headers)?.ToString() ?? "?"}")), Retry: false);
		}
		_log.Warn($"Release check for {repo}: unexpected HTTP {status}.");
		return (Value: ReleaseFeedResult.Fail(new ReleaseFeedError.Other(status, $"HTTP {status} {response.ReasonPhrase}")), Retry: false);
	}

	private static CachedRelease? UsableCache(LauncherSettings s, string repo)
	{
		if (string.IsNullOrWhiteSpace(s.LastEtag) || string.IsNullOrWhiteSpace(s.LastReleaseJson))
		{
			return null;
		}
		if (!string.Equals(s.LastReleaseRepo?.Trim(), repo, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		return new CachedRelease(s.LastEtag.Trim(), s.LastReleaseJson);
	}

	private static string? ReadEtag(HttpResponseMessage response)
	{
		if (response.Headers.TryGetValues("ETag", out IEnumerable<string> values))
		{
			string text = values.FirstOrDefault()?.Trim();
			if (!string.IsNullOrEmpty(text))
			{
				return text;
			}
		}
		return response.Headers.ETag?.ToString();
	}

	private static async Task<string?> ReadBodyAsync(HttpResponseMessage response, CancellationToken token)
	{
		long? contentLength = response.Content.Headers.ContentLength;
		if (contentLength.HasValue && contentLength.GetValueOrDefault() > 4194304)
		{
			return null;
		}
		string result;
		await using (Stream stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(continueOnCapturedContext: false))
		{
			using MemoryStream buffer = new MemoryStream();
			byte[] chunk = new byte[16384];
			while (true)
			{
				int num;
				if ((num = await stream.ReadAsync(chunk, token).ConfigureAwait(continueOnCapturedContext: false)) > 0)
				{
					if (buffer.Length + num > 4194304)
					{
						result = null;
						break;
					}
					buffer.Write(chunk, 0, num);
					continue;
				}
				result = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
				break;
			}
		}
		return result;
	}

	private async Task SaveAsync(Func<LauncherSettings, LauncherSettings> change, CancellationToken ct)
	{
		try
		{
			await _settings.UpdateAsync(change, ct).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			_log.Warn("Could not save the release cache to settings.", exception);
		}
	}
}
