using System.Collections.Generic;

namespace ActualArrow.Core.Models;

public sealed record PackHealth(InstallRecord? Record, IReadOnlyList<string> MissingOrResized)
{
	public bool IsInstalled => (object)Record?.Pack != null;

	public bool IsIntact
	{
		get
		{
			if (IsInstalled)
			{
				return MissingOrResized.Count == 0;
			}
			return false;
		}
	}
}
