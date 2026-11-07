using System;
using System.IO;

namespace ActualArrow.Core.Services.Install;

internal sealed class IncompleteMoveException : IOException
{
	public IncompleteMoveException(string message, Exception inner)
		: base(message, inner)
	{
	}
}
