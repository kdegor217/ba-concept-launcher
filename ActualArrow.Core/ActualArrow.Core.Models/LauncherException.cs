using System;
using System.Collections.Generic;

namespace ActualArrow.Core.Models;

public class LauncherException : Exception
{
	public string MessageKey { get; }

	public IReadOnlyList<object> MessageArgs { get; }

	public LauncherException(string messageKey, string message, Exception? inner = null, params object[] messageArgs)
		: base(message, inner)
	{
		MessageKey = messageKey;
		MessageArgs = messageArgs;
	}

	public StatusMessage ToStatusMessage()
	{
		return new StatusMessage(MessageKey, MessageArgs);
	}
}
