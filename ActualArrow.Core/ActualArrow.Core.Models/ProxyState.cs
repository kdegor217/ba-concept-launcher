using System.Collections.Generic;

namespace ActualArrow.Core.Models;

public sealed record ProxyState(IReadOnlyList<string> InRoot, IReadOnlyList<string> Stashed)
{
	public bool AnyInRoot => InRoot.Count > 0;
}
