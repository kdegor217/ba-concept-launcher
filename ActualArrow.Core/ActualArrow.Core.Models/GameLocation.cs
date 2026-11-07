namespace ActualArrow.Core.Models;

public sealed record GameLocation(string GameDir, GameDirSource Source, GameDirValidation Validation)
{
	public bool IsValid => Validation == GameDirValidation.Valid;
}
