using System;
using System.Globalization;
using System.Text.Json;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Services.GitHub;

internal static class ReleaseJson
{
	private static readonly JsonDocumentOptions Options = new JsonDocumentOptions
	{
		AllowTrailingCommas = false,
		CommentHandling = JsonCommentHandling.Disallow,
		MaxDepth = 64
	};

	private const int MaxNotesLength = 4000;

	public static ParsedRelease Parse(string repo, string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return Fail("empty body");
		}
		JsonDocument jsonDocument;
		try
		{
			jsonDocument = JsonDocument.Parse(json, Options);
		}
		catch (JsonException ex)
		{
			return Fail("invalid JSON: " + ex.Message);
		}
		using (jsonDocument)
		{
			JsonElement rootElement = jsonDocument.RootElement;
			if (rootElement.ValueKind != JsonValueKind.Object)
			{
				return Fail("release is not a JSON object");
			}
			string text = GetString(rootElement, "tag_name")?.Trim();
			if (string.IsNullOrEmpty(text))
			{
				return Fail("release has no tag_name");
			}
			if (GetBool(rootElement, "draft") == true || GetBool(rootElement, "prerelease") == true)
			{
				return Fail("release " + text + " is a draft or prerelease");
			}
			string text2 = GetString(rootElement, "name");
			DateTimeOffset? date = GetDate(rootElement, "published_at");
			Uri absoluteUri = GetAbsoluteUri(rootElement, "html_url");
			ReleaseAsset releaseAsset = PickAsset(rootElement, out string note);
			if ((object)releaseAsset == null)
			{
				return new ParsedRelease(null, new ReleaseFeedError.NoPackAsset("release " + text + ": " + note));
			}
			string text3 = GetString(rootElement, "body")?.Trim();
			if (text3 != null && text3.Length > 4000)
			{
				text3 = text3.Substring(0, 4000);
			}
			return new ParsedRelease(new ReleaseInfo(repo, text, string.IsNullOrWhiteSpace(text2) ? null : text2, date, absoluteUri, releaseAsset)
			{
				Notes = (string.IsNullOrWhiteSpace(text3) ? null : text3)
			}, null);
		}
	}

	private static ReleaseAsset? PickAsset(JsonElement root, out string note)
	{
		note = "no assets";
		if (!root.TryGetProperty("assets", out var value) || value.ValueKind != JsonValueKind.Array)
		{
			return null;
		}
		ReleaseAsset releaseAsset = null;
		int num = 0;
		foreach (JsonElement item in value.EnumerateArray())
		{
			ReleaseAsset releaseAsset2 = ReadAsset(item);
			if ((object)releaseAsset2 != null)
			{
				num++;
				if (releaseAsset2.Name.EndsWith(".bamod", StringComparison.OrdinalIgnoreCase))
				{
					return releaseAsset2;
				}
				if ((object)releaseAsset == null && releaseAsset2.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
				{
					releaseAsset = releaseAsset2;
				}
			}
		}
		note = ((num == 0) ? "no usable assets" : $"none of {num} assets is {".bamod"} or {".zip"}");
		return releaseAsset;
	}

	private static ReleaseAsset? ReadAsset(JsonElement item)
	{
		if (item.ValueKind != JsonValueKind.Object)
		{
			return null;
		}
		string text = GetString(item, "name")?.Trim();
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		string text2 = GetString(item, "state");
		if (text2 != null && !string.Equals(text2, "uploaded", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		Uri absoluteUri = GetAbsoluteUri(item, "browser_download_url");
		if ((object)absoluteUri == null || (absoluteUri.Scheme != Uri.UriSchemeHttps && absoluteUri.Scheme != Uri.UriSchemeHttp))
		{
			return null;
		}
		if (!item.TryGetProperty("size", out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var value2) || value2 < 0)
		{
			return null;
		}
		string sha = Sha256Hex.FromDigest(GetString(item, "digest"));
		return new ReleaseAsset(text, absoluteUri, value2, sha);
	}

	private static ParsedRelease Fail(string detail)
	{
		return new ParsedRelease(null, new ReleaseFeedError.Other(null, detail));
	}

	private static string? GetString(JsonElement obj, string property)
	{
		if (!obj.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
		{
			return null;
		}
		return value.GetString();
	}

	private static bool? GetBool(JsonElement obj, string property)
	{
		bool flag = obj.TryGetProperty(property, out var value);
		if (flag)
		{
			JsonValueKind valueKind = value.ValueKind;
			bool flag2 = valueKind - 5 <= JsonValueKind.Object;
			flag = flag2;
		}
		if (!flag)
		{
			return null;
		}
		return value.GetBoolean();
	}

	private static DateTimeOffset? GetDate(JsonElement obj, string property)
	{
		if (!DateTimeOffset.TryParse(GetString(obj, property), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var result))
		{
			return null;
		}
		return result;
	}

	private static Uri? GetAbsoluteUri(JsonElement obj, string property)
	{
		if (!Uri.TryCreate(GetString(obj, property), UriKind.Absolute, out Uri result))
		{
			return null;
		}
		return result;
	}
}
