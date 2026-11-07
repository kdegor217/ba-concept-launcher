using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace ActualArrow.Core.Services.GitHub;

internal static class RateLimitHeaders
{
	public static bool IsRateLimited(HttpResponseMessage response)
	{
		HttpStatusCode statusCode = response.StatusCode;
		if (statusCode != HttpStatusCode.Forbidden && statusCode != HttpStatusCode.TooManyRequests)
		{
			return false;
		}
		if (statusCode == HttpStatusCode.TooManyRequests)
		{
			return true;
		}
		if (Remaining(response.Headers) == 0)
		{
			return true;
		}
		return response.Headers.RetryAfter != null;
	}

	public static DateTimeOffset? ResetTime(HttpResponseMessage response, DateTimeOffset now)
	{
		DateTimeOffset? dateTimeOffset = null;
		if (long.TryParse(First(response.Headers, "x-ratelimit-reset"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && result > 0 && result < 253402300799L)
		{
			dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(result);
		}
		RetryConditionHeaderValue retryAfter = response.Headers.RetryAfter;
		DateTimeOffset? dateTimeOffset2;
		if (retryAfter != null)
		{
			TimeSpan? delta = retryAfter.Delta;
			if (delta.HasValue)
			{
				TimeSpan valueOrDefault = delta.GetValueOrDefault();
				if (valueOrDefault >= TimeSpan.Zero)
				{
					dateTimeOffset2 = now + valueOrDefault;
					goto IL_00c8;
				}
			}
			DateTimeOffset? date = retryAfter.Date;
			if (date.HasValue)
			{
				dateTimeOffset2 = date.GetValueOrDefault().ToUniversalTime();
				goto IL_00c8;
			}
		}
		dateTimeOffset2 = null;
		goto IL_00c8;
		IL_00c8:
		DateTimeOffset? dateTimeOffset3 = dateTimeOffset2;
		if (!dateTimeOffset.HasValue)
		{
			return dateTimeOffset3;
		}
		if (!dateTimeOffset3.HasValue)
		{
			return dateTimeOffset;
		}
		if (!(dateTimeOffset > dateTimeOffset3))
		{
			return dateTimeOffset3;
		}
		return dateTimeOffset;
	}

	public static long? Remaining(HttpResponseHeaders headers)
	{
		if (!long.TryParse(First(headers, "x-ratelimit-remaining"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return null;
		}
		return result;
	}

	private static string? First(HttpResponseHeaders headers, string name)
	{
		if (!headers.TryGetValues(name, out IEnumerable<string> values))
		{
			return null;
		}
		return values.FirstOrDefault()?.Trim();
	}
}
