using System;

namespace ActualArrow.Core.Services.GitHub;

internal static class HostPolicy
{
	public static bool IsAllowed(Uri? uri)
	{
		if ((object)uri == null || !uri.IsAbsoluteUri)
		{
			return false;
		}
		if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (!uri.IsDefaultPort)
		{
			return false;
		}
		if (!string.IsNullOrEmpty(uri.UserInfo))
		{
			return false;
		}
		string a = uri.IdnHost.TrimEnd('.');
		foreach (string allowedHost in Brand.GitHub.AllowedHosts)
		{
			if (string.Equals(a, allowedHost, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsApiHost(Uri? uri)
	{
		if (IsAllowed(uri))
		{
			return string.Equals(uri.IdnHost.TrimEnd('.'), new Uri("https://api.github.com/").Host, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	public static string ForLog(Uri? uri)
	{
		if ((object)uri == null)
		{
			return "(null)";
		}
		if (!uri.IsAbsoluteUri)
		{
			return uri.OriginalString;
		}
		return uri.GetLeftPart(UriPartial.Path);
	}
}
