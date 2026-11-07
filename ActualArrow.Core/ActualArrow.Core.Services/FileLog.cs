using System;
using System.Globalization;
using System.IO;
using System.Text;
using ActualArrow.Core.Abstractions;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Services;

public sealed class FileLog : ILog
{
	public const string RotatedSuffix = ".old";

	private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	private readonly string _path;

	private readonly TimeProvider _time;

	private readonly long _limit;

	private readonly object _gate = new object();

	public string? FilePath => _path;

	public string RotatedFilePath => _path + ".old";

	public FileLog(IAppPaths paths, TimeProvider? time = null)
		: this(paths, time, 1048576L)
	{
	}

	internal FileLog(IAppPaths paths, TimeProvider? time, long rotateBytes)
	{
		ArgumentNullException.ThrowIfNull(paths, "paths");
		_path = paths.LogFile;
		_time = time ?? TimeProvider.System;
		_limit = Math.Max(1L, rotateBytes);
	}

	public void Write(LogLevel level, string message, Exception? exception = null)
	{
		string s;
		try
		{
			s = Format(_time.GetUtcNow(), level, message, exception);
		}
		catch
		{
			return;
		}
		byte[] bytes = Utf8NoBom.GetBytes(s);
		lock (_gate)
		{
			try
			{
				string directoryName = Path.GetDirectoryName(_path);
				if (!string.IsNullOrEmpty(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
				FileInfo fileInfo = new FileInfo(_path);
				if (fileInfo.Exists && fileInfo.Length > 0 && fileInfo.Length + bytes.Length > _limit)
				{
					Rotate();
				}
				using FileStream fileStream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
				fileStream.Write(bytes, 0, bytes.Length);
			}
			catch
			{
			}
		}
	}

	private void Rotate()
	{
		try
		{
			File.Move(_path, RotatedFilePath, overwrite: true);
		}
		catch (IOException)
		{
			using (new FileStream(_path, FileMode.Truncate, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
			{
			}
		}
	}

	internal static string Format(DateTimeOffset utcNow, LogLevel level, string? message, Exception? exception)
	{
		StringBuilder stringBuilder = new StringBuilder(128);
		stringBuilder.Append(utcNow.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
		stringBuilder.Append("  ").Append(LevelTag(level)).Append("  ");
		AppendIndented(stringBuilder, message ?? string.Empty);
		if (exception != null)
		{
			stringBuilder.Append('\n').Append("    ");
			AppendIndented(stringBuilder, exception.ToString());
		}
		stringBuilder.Append('\n');
		return stringBuilder.ToString();
	}

	private static string LevelTag(LogLevel level)
	{
		return level switch
		{
			LogLevel.Debug => "DEBUG", 
			LogLevel.Info => "INFO ", 
			LogLevel.Warning => "WARN ", 
			LogLevel.Error => "ERROR", 
			_ => "?????", 
		};
	}

	private static void AppendIndented(StringBuilder sb, string text)
	{
		string text2 = text.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n');
		bool flag = true;
		string[] array = text2.Split('\n');
		foreach (string value in array)
		{
			if (!flag)
			{
				sb.Append('\n').Append("    ");
			}
			sb.Append(value);
			flag = false;
		}
	}
}
