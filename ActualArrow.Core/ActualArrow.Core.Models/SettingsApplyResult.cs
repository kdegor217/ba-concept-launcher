using System;
using System.Collections.Generic;

namespace ActualArrow.Core.Models;

public sealed record SettingsApplyResult(IReadOnlyList<StatusMessage> Errors)
{
	public bool IsValid => Errors.Count == 0;

	public static SettingsApplyResult Saved { get; } = new SettingsApplyResult(Array.Empty<StatusMessage>());
}
