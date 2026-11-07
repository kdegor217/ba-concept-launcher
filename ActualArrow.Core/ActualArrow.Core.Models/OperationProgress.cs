using System;

namespace ActualArrow.Core.Models;

public sealed record OperationProgress(OperationKind Kind, long? BytesDone = null, long? BytesTotal = null, int? ItemsDone = null, int? ItemsTotal = null)
{
	public double? Fraction
	{
		get
		{
			long? bytesTotal = BytesTotal;
			if (!bytesTotal.HasValue || bytesTotal.GetValueOrDefault() <= 0 || !BytesDone.HasValue)
			{
				int? itemsTotal = ItemsTotal;
				if (!itemsTotal.HasValue || itemsTotal.GetValueOrDefault() <= 0 || !ItemsDone.HasValue)
				{
					return null;
				}
				return Math.Clamp((double)ItemsDone.Value / (double)ItemsTotal.Value, 0.0, 1.0);
			}
			return Math.Clamp((double)BytesDone.Value / (double)BytesTotal.Value, 0.0, 1.0);
		}
	}
}
