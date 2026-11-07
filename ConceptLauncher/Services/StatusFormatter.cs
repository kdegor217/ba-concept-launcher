using ActualArrow.Core.Models;

namespace ConceptApp.Services;

public static class StatusFormatter
{
	public static string Format(Loc loc, StatusMessage? message)
	{
		if (message is null)
			return loc.T("Ui.Ready");

		var args = message.Args.Select(a => (object)(a?.ToString() ?? "")).ToArray();
		var text = loc.T(message.Key, args);
		if (!string.Equals(text, message.Key, StringComparison.Ordinal))
			return text;

		return args.Length == 0
			? message.Key
			: $"{message.Key}: {string.Join(", ", args.Select(a => a.ToString()))}";
	}

	public static double ProgressPercent(OperationProgress? progress)
	{
		if (progress is null)
			return 0;

		if (progress.BytesTotal is > 0 && progress.BytesDone is not null)
			return Math.Clamp(100.0 * progress.BytesDone.Value / progress.BytesTotal.Value, 0, 100);

		if (progress.ItemsTotal is > 0 && progress.ItemsDone is not null)
			return Math.Clamp(100.0 * progress.ItemsDone.Value / progress.ItemsTotal.Value, 0, 100);

		return progress.Kind switch
		{
			OperationKind.DownloadingMelonLoader or OperationKind.DownloadingPack => 15,
			OperationKind.InstallingMelonLoader or OperationKind.InstallingPack => 55,
			OperationKind.Launching or OperationKind.WaitingForSteam => 85,
			_ => 30
		};
	}
}
