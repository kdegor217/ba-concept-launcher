namespace ActualArrow.Core.Models;

public sealed record SettingsChange(string? GameDir, string? PackRepo, string Language, bool CloseOnLaunch);
