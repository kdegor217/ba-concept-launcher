using System;
using System.Collections.Generic;
using System.Text;
using ActualArrow.Core.Models;

namespace ActualArrow.Core.Services;

public static class VdfParser
{
	private enum TokenKind
	{
		End,
		String,
		Open,
		Close,
		Conditional
	}

	private readonly record struct Token(TokenKind Kind, string Text, int Line, int Column);

	private ref struct Lexer
	{
		private readonly ReadOnlySpan<char> _s;

		private int _pos;

		private int _line;

		private int _lineStart;

		public Lexer(string text)
		{
			_s = text;
			_pos = ((_s.Length > 0 && _s[0] == '\ufeff') ? 1 : 0);
			_line = 1;
			_lineStart = 0;
		}

		public FormatException Error(string message, Token? at = null)
		{
			int value = at?.Line ?? _line;
			int value2 = at?.Column ?? (_pos - _lineStart + 1);
			return new FormatException($"vdf: {message} (line {value}, column {value2})");
		}

		public void SkipConditional()
		{
			(int, int, int) tuple = (_pos, _line, _lineStart);
			if (Next().Kind != TokenKind.Conditional)
			{
				(_pos, _line, _lineStart) = tuple;
			}
		}

		public Token Next()
		{
			SkipTrivia();
			if (_pos >= _s.Length)
			{
				return new Token(TokenKind.End, string.Empty, _line, _pos - _lineStart + 1);
			}
			int line = _line;
			int num = _pos - _lineStart + 1;
			switch (_s[_pos])
			{
			case '{':
				_pos++;
				return new Token(TokenKind.Open, "{", line, num);
			case '}':
				_pos++;
				return new Token(TokenKind.Close, "}", line, num);
			case '"':
				return new Token(TokenKind.String, ReadQuoted(line, num), line, num);
			case '[':
			{
				ReadOnlySpan<char> s = _s;
				int pos = _pos;
				int num2 = s.Slice(pos, s.Length - pos).IndexOfAny(']', '\n');
				if (num2 > 0 && _s[_pos + num2] == ']')
				{
					string text = _s.Slice(_pos, num2 + 1).ToString();
					_pos += num2 + 1;
					return new Token(TokenKind.Conditional, text, line, num);
				}
				break;
			}
			}
			return new Token(TokenKind.String, ReadUnquoted(), line, num);
		}

		private void SkipTrivia()
		{
			while (_pos < _s.Length)
			{
				char c = _s[_pos];
				switch (c)
				{
				case '\n':
					_pos++;
					NewLine();
					continue;
				case '\r':
					_pos++;
					if (_pos < _s.Length && _s[_pos] == '\n')
					{
						_pos++;
					}
					NewLine();
					continue;
				}
				if (char.IsWhiteSpace(c))
				{
					_pos++;
					continue;
				}
				if (c == '/' && _pos + 1 < _s.Length && _s[_pos + 1] == '/')
				{
					while (_pos < _s.Length && _s[_pos] != '\n' && _s[_pos] != '\r')
					{
						_pos++;
					}
					continue;
				}
				return;
			}
		}

		private void NewLine()
		{
			_line++;
			_lineStart = _pos;
		}

		private string ReadQuoted(int line, int col)
		{
			_pos++;
			StringBuilder stringBuilder = new StringBuilder();
			while (_pos < _s.Length)
			{
				char c = _s[_pos++];
				switch (c)
				{
				case '"':
					return stringBuilder.ToString();
				case '\\':
					if (_pos < _s.Length)
					{
						switch (_s[_pos++])
						{
						case '\\':
							stringBuilder.Append('\\');
							break;
						case '"':
							stringBuilder.Append('"');
							break;
						case 'n':
							stringBuilder.Append('\n');
							break;
						case 't':
							stringBuilder.Append('\t');
							break;
						case 'r':
							stringBuilder.Append('\r');
							break;
						default:
							stringBuilder.Append('\\');
							_pos--;
							break;
						}
						continue;
					}
					break;
				}
				switch (c)
				{
				case '\n':
					NewLine();
					break;
				case '\r':
					if (_pos < _s.Length && _s[_pos] == '\n')
					{
						stringBuilder.Append('\r');
						c = _s[_pos++];
					}
					NewLine();
					break;
				}
				stringBuilder.Append(c);
			}
			throw new FormatException($"vdf: unterminated quoted string (line {line}, column {col})");
		}

		private string ReadUnquoted()
		{
			int pos = _pos;
			while (_pos < _s.Length)
			{
				char c = _s[_pos];
				bool flag = char.IsWhiteSpace(c);
				if (!flag)
				{
					bool flag2 = ((c == '"' || c == '{' || c == '}') ? true : false);
					flag = flag2;
				}
				if (flag)
				{
					break;
				}
				_pos++;
			}
			ReadOnlySpan<char> s = _s;
			int num = pos;
			return s.Slice(num, _pos - num).ToString();
		}
	}

	private const int MaxDepth = 256;

	public static VdfNode Parse(string text)
	{
		ArgumentNullException.ThrowIfNull(text, "text");
		if (text.Contains('\0'))
		{
			throw new FormatException("vdf: NUL character found; binary KeyValues are not supported");
		}
		Lexer lexer = new Lexer(text);
		List<VdfNode> children = ParseBlockBody(ref lexer, 0, topLevel: true);
		return new VdfNode(string.Empty, null, children);
	}

	private static List<VdfNode> ParseBlockBody(ref Lexer lexer, int depth, bool topLevel)
	{
		if (depth > 256)
		{
			throw lexer.Error("nesting is too deep");
		}
		List<VdfNode> list = new List<VdfNode>();
		while (true)
		{
			Token value = lexer.Next();
			switch (value.Kind)
			{
			case TokenKind.Conditional:
				continue;
			case TokenKind.End:
				return list;
			case TokenKind.Close:
				if (topLevel)
				{
					throw lexer.Error("unexpected '}' at top level", value);
				}
				return list;
			case TokenKind.Open:
				throw lexer.Error("'{' where a key was expected", value);
			}
			string text = value.Text;
			Token value2 = lexer.Next();
			while (value2.Kind == TokenKind.Conditional)
			{
				value2 = lexer.Next();
			}
			switch (value2.Kind)
			{
			case TokenKind.String:
				list.Add(new VdfNode(text, value2.Text));
				lexer.SkipConditional();
				break;
			case TokenKind.Open:
			{
				List<VdfNode> children = ParseBlockBody(ref lexer, depth + 1, topLevel: false);
				list.Add(new VdfNode(text, null, children));
				break;
			}
			case TokenKind.End:
				throw lexer.Error("key \"" + text + "\" has no value", value);
			case TokenKind.Close:
				throw lexer.Error("key \"" + text + "\" is followed by '}' instead of a value", value2);
			}
		}
	}
}
