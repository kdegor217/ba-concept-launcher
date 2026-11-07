using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace ActualArrow.Core.Services.Basics;

internal sealed record SteamProbeEnvironment(bool IsWindows, string? Home, Func<bool, string, string, string?> ReadRegistry, string? ProgramFilesX86, string? WineUnixHome = null)
{
	public static SteamProbeEnvironment Current()
	{
		if (OperatingSystem.IsWindows())
		{
			return new SteamProbeEnvironment(IsWindows: true, PathText.HomeDirectory(), ReadWindowsRegistry, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolderOption.DoNotVerify), DetectWineUnixHome());
		}
		return new SteamProbeEnvironment(IsWindows: false, PathText.HomeDirectory(), (bool _, string _, string _) => (string?)null, null);
	}

	[SupportedOSPlatform("windows")]
	private static string? DetectWineUnixHome()
	{
		try
		{
			using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Wine", writable: false))
			{
				if (registryKey == null)
				{
					return null;
				}
			}
			string environmentVariable = Environment.GetEnvironmentVariable("HOME");
			if (environmentVariable != null && environmentVariable.Length > 1 && environmentVariable[0] == '/')
			{
				return environmentVariable;
			}
			string userName = Environment.UserName;
			if (string.IsNullOrWhiteSpace(userName))
			{
				return null;
			}
			string path = "Z:\\etc\\passwd";
			return (File.Exists(path) ? HomeFromPasswd(File.ReadLines(path), userName) : null) ?? ("/home/" + userName);
		}
		catch (Exception ex) when ((ex is SecurityException || ex is UnauthorizedAccessException || ex is IOException) ? true : false)
		{
			return null;
		}
	}

	internal static string? HomeFromPasswd(IEnumerable<string> lines, string user)
	{
		foreach (string line in lines)
		{
			string[] array = line.Split(':');
			if (array.Length >= 6 && string.Equals(array[0], user, StringComparison.Ordinal) && array[5].StartsWith('/'))
			{
				return array[5];
			}
		}
		return null;
	}

	[SupportedOSPlatform("windows")]
	private static string? ReadWindowsRegistry(bool machine, string keyPath, string valueName)
	{
		try
		{
			using RegistryKey registryKey = (machine ? Registry.LocalMachine : Registry.CurrentUser).OpenSubKey(keyPath, writable: false);
			return registryKey?.GetValue(valueName) as string;
		}
		catch (Exception ex) when ((ex is SecurityException || ex is UnauthorizedAccessException || ex is IOException) ? true : false)
		{
			return null;
		}
	}
}
