namespace ActualArrow.Core.Models;

public sealed record Banner(BannerSeverity Severity, StatusMessage Message, StatusMessage? Detail = null);
