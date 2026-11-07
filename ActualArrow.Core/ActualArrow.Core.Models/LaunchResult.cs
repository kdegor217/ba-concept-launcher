using System;

namespace ActualArrow.Core.Models;

public sealed record LaunchResult(LaunchMethod Method, bool DryRun, int? ProcessId, DateTimeOffset StartedUtc, string Description);
