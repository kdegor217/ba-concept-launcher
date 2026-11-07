using System;
using ActualArrow.Core.Abstractions;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Services;

public static class LogExtensions
{
	public static void Debug(this ILog log, string message)
	{
		log.Write(LogLevel.Debug, message);
	}

	public static void Info(this ILog log, string message)
	{
		log.Write(LogLevel.Info, message);
	}

	public static void Warn(this ILog log, string message, Exception? exception = null)
	{
		log.Write(LogLevel.Warning, message, exception);
	}

	public static void Error(this ILog log, string message, Exception? exception = null)
	{
		log.Write(LogLevel.Error, message, exception);
	}
}
