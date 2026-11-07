using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace ActualArrow.Core.Services.Runner;

public sealed class SystemProcessHost : IProcessHost
{
	public static SystemProcessHost Instance { get; } = new SystemProcessHost();

	public bool IsRunning(string processName)
	{
		string b = ProcessNames.Normalize(processName);
		Process[] processes;
		try
		{
			processes = Process.GetProcesses();
		}
		catch (Exception)
		{
			return false;
		}
		bool flag = false;
		Process[] array = processes;
		foreach (Process process in array)
		{
			try
			{
				if (!flag && string.Equals(ProcessNames.Normalize(process.ProcessName), b, StringComparison.OrdinalIgnoreCase))
				{
					flag = true;
				}
			}
			catch (Exception)
			{
			}
			finally
			{
				process.Dispose();
			}
		}
		return flag;
	}

	public int? Start(ProcessLaunchSpec spec)
	{
		ProcessStartInfo processStartInfo = new ProcessStartInfo(spec.FileName)
		{
			WorkingDirectory = spec.WorkingDirectory,
			UseShellExecute = false
		};
		foreach (var (key, value) in spec.Environment)
		{
			processStartInfo.Environment[key] = value;
		}
		foreach (string item in spec.Arguments ?? Array.Empty<string>())
		{
			processStartInfo.ArgumentList.Add(item);
		}
		using Process process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("Process.Start returned no process.");
		try
		{
			return process.Id;
		}
		catch (InvalidOperationException)
		{
			return null;
		}
	}

	public bool IsAlive(int processId)
	{
		try
		{
			using Process process = Process.GetProcessById(processId);
			return !process.HasExited;
		}
		catch (Exception ex) when ((ex is ArgumentException || ex is InvalidOperationException || ex is Win32Exception || ex is NotSupportedException) ? true : false)
		{
			return false;
		}
	}

	public void ShellOpen(string target)
	{
		ProcessStartInfo processStartInfo;
		if (OperatingSystem.IsWindows())
		{
			processStartInfo = new ProcessStartInfo(target)
			{
				UseShellExecute = true
			};
		}
		else
		{
			processStartInfo = new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open")
			{
				UseShellExecute = false
			};
			processStartInfo.ArgumentList.Add(target);
		}
		using (Process.Start(processStartInfo))
		{
		}
	}
}
