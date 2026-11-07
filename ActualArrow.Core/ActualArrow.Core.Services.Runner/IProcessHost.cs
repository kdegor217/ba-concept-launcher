namespace ActualArrow.Core.Services.Runner;

public interface IProcessHost
{
	bool IsRunning(string processName);

	int? Start(ProcessLaunchSpec spec);

	bool IsAlive(int processId)
	{
		return false;
	}

	void ShellOpen(string target);
}
