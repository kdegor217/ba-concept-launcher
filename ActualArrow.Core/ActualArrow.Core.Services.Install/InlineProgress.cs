using System;

namespace ActualArrow.Core.Services.Install;

internal sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
{
	public void Report(T value)
	{
		report(value);
	}
}
