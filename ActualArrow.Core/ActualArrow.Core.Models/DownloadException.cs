using System;

namespace ActualArrow.Core.Models;

public sealed class DownloadException : LauncherException
{
	public DownloadFailure Failure { get; }

	public int? StatusCode { get; }

	public DownloadException(DownloadFailure failure, string message, int? statusCode = null, Exception? inner = null)
		: base(KeyFor(failure), message, inner)
	{
		Failure = failure;
		StatusCode = statusCode;
	}

	private static string KeyFor(DownloadFailure failure)
	{
		switch (failure)
		{
		case DownloadFailure.SizeMismatch:
			return "Error.SizeMismatch";
		case DownloadFailure.HashMismatch:
			return "Error.HashMismatch";
		case DownloadFailure.Network:
		case DownloadFailure.Timeout:
			return "Error.DownloadStalled";
		default:
			return "Error.DownloadFailed";
		}
	}
}
