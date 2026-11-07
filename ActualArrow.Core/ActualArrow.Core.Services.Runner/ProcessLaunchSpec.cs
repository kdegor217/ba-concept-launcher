using System.Collections.Generic;
using System.Linq;

namespace ActualArrow.Core.Services.Runner;

public sealed record ProcessLaunchSpec(string FileName, string WorkingDirectory, IReadOnlyDictionary<string, string> Environment, IReadOnlyList<string>? Arguments = null)
{
	public string Describe()
	{
		string value = ((Environment.Count == 0) ? "" : (" env[" + string.Join(", ", Environment.Select((KeyValuePair<string, string> kv) => kv.Key + "=" + kv.Value)) + "]"));
		IReadOnlyList<string> arguments = Arguments;
		string value2 = ((arguments != null && arguments.Count > 0) ? (" " + string.Join(" ", arguments.Select((string x) => (!x.Contains(' ')) ? x : ("\"" + x + "\"")))) : "");
		return $"\"{FileName}\"{value2} cwd=\"{WorkingDirectory}\"{value}";
	}
}
