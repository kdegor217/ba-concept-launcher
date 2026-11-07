using System;
using System.Collections.Generic;
using System.Linq;

namespace ActualArrow.Core.Models;

public sealed class VdfNode
{
	public string Key { get; }

	public string? Value { get; }

	public IReadOnlyList<VdfNode> Children { get; }

	public bool IsBlock => Value == null;

	public VdfNode? this[string key] => Children.FirstOrDefault((VdfNode c) => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

	public VdfNode(string key, string? value = null, IReadOnlyList<VdfNode>? children = null)
	{
		Key = key;
		Value = value;
		Children = children ?? Array.Empty<VdfNode>();
	}

	public string? GetValue(string key)
	{
		return this[key]?.Value;
	}

	public override string ToString()
	{
		if (IsBlock)
		{
			return $"\"{Key}\" {{ {Children.Count} }}";
		}
		return $"\"{Key}\" \"{Value}\"";
	}
}
