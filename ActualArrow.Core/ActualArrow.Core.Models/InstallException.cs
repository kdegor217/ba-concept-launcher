using System;

namespace ActualArrow.Core.Models;

public sealed class InstallException : LauncherException
{
	public InstallFailure Failure { get; }

	public InstallException(InstallFailure failure, string message, Exception? inner = null)
		: base(KeyFor(failure), message, inner)
	{
		Failure = failure;
	}

	private static string KeyFor(InstallFailure failure)
	{
		return failure switch
		{
			InstallFailure.InvalidPack => "Error.InvalidPack", 
			InstallFailure.UnrecordedMelonLoader => "Error.MelonLoaderFailed", 
			InstallFailure.InvalidMelonLoader => "Error.MelonLoaderFailed", 
			InstallFailure.RollbackFailed => "Error.RollbackFailed", 
			InstallFailure.NotInstalled => "Error.NotInstalled", 
			InstallFailure.RecordUnreadable => "Error.RecordUnreadable", 
			_ => "Error.InstallFailed", 
		};
	}
}
