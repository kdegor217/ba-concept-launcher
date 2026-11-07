namespace ActualArrow.Core.Models;

public sealed record PackCatalogEntry(string Id, string Name, string Repo, string? Author = null, string? Description = null);
