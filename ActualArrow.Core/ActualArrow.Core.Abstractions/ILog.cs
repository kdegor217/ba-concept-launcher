using System;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Abstractions;

public interface ILog
{
	string? FilePath { get; }

	void Write(LogLevel level, string message, Exception? exception = null);
}
