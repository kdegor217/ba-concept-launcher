namespace ActualArrow.Core.Models;

public sealed record OperationResult(OperationOutcome Outcome, StatusMessage? Message = null, bool GameStarted = false)
{
	public static OperationResult Ok { get; } = new OperationResult(OperationOutcome.Succeeded);

	public static OperationResult Busy { get; } = new OperationResult(OperationOutcome.Busy);

	public static OperationResult Cancelled { get; } = new OperationResult(OperationOutcome.Cancelled, StatusMessage.Of("Status.Cancelled"));
}
