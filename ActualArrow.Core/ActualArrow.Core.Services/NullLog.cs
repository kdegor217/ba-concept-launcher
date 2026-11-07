using System;
using ActualArrow.Core.Abstractions;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Services;

public sealed class NullLog : ILog
{
	public static NullLog Instance { get; } = new NullLog();

	public string? FilePath => null;

	public void Write(LogLevel level, string message, Exception? exception = null)
	{
	}
}
