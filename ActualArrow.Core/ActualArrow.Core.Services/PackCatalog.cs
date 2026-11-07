using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Abstractions;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Services;

public sealed class PackCatalog : IPackCatalog
{
	private static readonly Regex RepoRegex = new Regex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})/[A-Za-z0-9._-]{1,100}$", RegexOptions.CultureInvariant);

	private readonly HttpClient _http;

	private readonly IAppPaths _paths;

	private readonly ILog _log;

	private readonly TimeProvider _time;

	private readonly string _repo;

	private IReadOnlyList<PackCatalogEntry>? _fetched;

	public static IReadOnlyList<PackCatalogEntry> BuiltIn { get; } = new _003C_003Ez__ReadOnlySingleElementList<PackCatalogEntry>(new PackCatalogEntry("wlfk.leha_arrow_mods", "ActualArrow", "wow-png/Leha_Arrow_mods"));

	private string CacheFile => Path.Combine(_paths.Root, "catalog.json");

	public PackCatalog(HttpClient http, IAppPaths paths, ILog log, TimeProvider? time = null, string? repo = null)
	{
		_http = http ?? throw new ArgumentNullException("http");
		_paths = paths ?? throw new ArgumentNullException("paths");
		_log = log ?? NullLog.Instance;
		_time = time ?? TimeProvider.System;
		_repo = repo ?? "aurka1823/ActualArrow";
	}

	public async Task<IReadOnlyList<PackCatalogEntry>> GetAsync(CancellationToken ct = default(CancellationToken))
	{
		IReadOnlyList<PackCatalogEntry> fetched = _fetched;
		if (fetched != null)
		{
			return fetched;
		}
		string text = await FetchAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		if (text != null)
		{
			string text2 = text;
			IReadOnlyList<PackCatalogEntry> readOnlyList = Parse(text2, _log);
			if (readOnlyList != null && readOnlyList.Count > 0)
			{
				TrySaveCache(text2);
				_log.Info($"Pack catalog: {readOnlyList.Count} pack(s) from {_repo}");
				return _fetched = readOnlyList;
			}
		}
		string text3 = TryReadCache();
		if (text3 != null)
		{
			IReadOnlyList<PackCatalogEntry> readOnlyList2 = Parse(text3, _log);
			if (readOnlyList2 != null && readOnlyList2.Count > 0)
			{
				_log.Info($"Pack catalog: {readOnlyList2.Count} pack(s) from the cached copy");
				return readOnlyList2;
			}
		}
		_log.Info("Pack catalog: built-in default only");
		return BuiltIn;
	}

	public static IReadOnlyList<PackCatalogEntry>? Parse(string json, ILog? log = null)
	{
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(json, new JsonDocumentOptions
			{
				AllowTrailingCommas = true,
				CommentHandling = JsonCommentHandling.Skip
			});
			if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object || !jsonDocument.RootElement.TryGetProperty("packs", out var value) || value.ValueKind != JsonValueKind.Array)
			{
				return null;
			}
			List<PackCatalogEntry> list = new List<PackCatalogEntry>();
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (JsonElement item in value.EnumerateArray())
			{
				if (list.Count >= 20)
				{
					break;
				}
				if (item.ValueKind == JsonValueKind.Object)
				{
					string text = Text(item, "repo", 140);
					string text2 = Text(item, "name", 60);
					if (text == null || text2 == null || !RepoRegex.IsMatch(text) || text.Contains("..", StringComparison.Ordinal))
					{
						log?.Warn($"Pack catalog: entry skipped (repo «{text}», name «{text2}»)");
					}
					else if (hashSet.Add(text))
					{
						list.Add(new PackCatalogEntry(Text(item, "id", 100) ?? text, text2, text, Text(item, "author", 60), Text(item, "description", 300)));
					}
				}
			}
			return list;
		}
		catch (JsonException exception)
		{
			log?.Warn("Pack catalog is not valid JSON", exception);
			return null;
		}
	}

	private static string? Text(JsonElement item, string name, int max)
	{
		if (!item.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
		{
			return null;
		}
		string text = value.GetString()?.Trim();
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		text = new string(text.Where((char c) => !char.IsControl(c)).ToArray());
		if (text.Length <= max)
		{
			return text;
		}
		return text.Substring(0, max);
	}

	private async Task<string?> FetchAsync(CancellationToken ct)
	{
		using CancellationTokenSource timeout = new CancellationTokenSource(Brand.GitHub.CheckTimeout, _time);
		using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
		_ = 3;
		try
		{
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "repos/" + _repo + "/contents/catalog.json");
			request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.raw"));
			using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(continueOnCapturedContext: false);
			if (!response.IsSuccessStatusCode)
			{
				_log.Info($"Pack catalog: {(int)response.StatusCode} from {_repo}");
				return null;
			}
			long? contentLength = response.Content.Headers.ContentLength;
			if (contentLength.HasValue && contentLength.GetValueOrDefault() > 65536)
			{
				return null;
			}
			string result;
			await using (Stream stream = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(continueOnCapturedContext: false))
			{
				byte[] buffer = new byte[65537];
				int read = 0;
				int n = default;
				while (true)
				{
					bool flag = read < buffer.Length;
					if (flag)
					{
						int num;
						n = (num = await stream.ReadAsync(buffer.AsMemory(read), linked.Token).ConfigureAwait(continueOnCapturedContext: false));
						flag = num > 0;
					}
					if (!flag)
					{
						break;
					}
					read += n;
				}
				result = ((read > 65536) ? null : Encoding.UTF8.GetString(buffer, 0, read));
			}
			return result;
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex2) when ((ex2 is HttpRequestException || ex2 is OperationCanceledException || ex2 is IOException) ? true : false)
		{
			_log.Info("Pack catalog not reachable: " + ex2.Message);
			return null;
		}
	}

	private string? TryReadCache()
	{
		try
		{
			return File.Exists(CacheFile) ? File.ReadAllText(CacheFile, Encoding.UTF8) : null;
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			return null;
		}
	}

	private void TrySaveCache(string text)
	{
		try
		{
			Directory.CreateDirectory(_paths.Root);
			string text2 = CacheFile + ".tmp";
			File.WriteAllText(text2, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			File.Move(text2, CacheFile, overwrite: true);
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			_log.Warn("Could not cache the pack catalog", ex);
		}
	}
}
