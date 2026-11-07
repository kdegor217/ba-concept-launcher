using System;

namespace ActualArrow.Core.Models;

public readonly record struct DownloadProgress(long BytesDone, long? BytesTotal)
{
	public double? Fraction
	{
		get
		{
			long? bytesTotal = BytesTotal;
			if (!bytesTotal.HasValue || bytesTotal.GetValueOrDefault() <= 0)
			{
				return null;
			}
			return Math.Clamp((double)BytesDone / (double)BytesTotal.Value, 0.0, 1.0);
		}
	}
}
