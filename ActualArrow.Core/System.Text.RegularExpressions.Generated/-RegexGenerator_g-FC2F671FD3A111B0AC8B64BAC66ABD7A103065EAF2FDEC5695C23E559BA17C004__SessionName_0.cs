using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.Generated;

[GeneratedCode("System.Text.RegularExpressions.Generator", "42.42.42.42")]
internal sealed class _003CRegexGenerator_g_003EFC2F671FD3A111B0AC8B64BAC66ABD7A103065EAF2FDEC5695C23E559BA17C004__SessionName_0 : Regex
{
	private sealed class RunnerFactory : RegexRunnerFactory
	{
		private sealed class Runner : RegexRunner
		{
			protected override void Scan(ReadOnlySpan<char> inputSpan)
			{
				if (TryFindNextPossibleStartingPosition(inputSpan) && !TryMatchAtCurrentPosition(inputSpan))
				{
					runtextpos = inputSpan.Length;
				}
			}

			private bool TryFindNextPossibleStartingPosition(ReadOnlySpan<char> inputSpan)
			{
				int num = runtextpos;
				if (num <= inputSpan.Length - 15 && num == 0)
				{
					return true;
				}
				runtextpos = inputSpan.Length;
				return false;
			}

			private bool TryMatchAtCurrentPosition(ReadOnlySpan<char> inputSpan)
			{
				int num = runtextpos;
				int start = num;
				int num2 = 0;
				int num3 = 0;
				int pos = 0;
				ReadOnlySpan<char> readOnlySpan = inputSpan.Slice(num);
				if (num != 0)
				{
					UncaptureUntil(0);
					return false;
				}
				num2 = num;
				if ((uint)readOnlySpan.Length < 15u || !char.IsDigit(readOnlySpan[0]) || !char.IsDigit(readOnlySpan[1]) || !char.IsDigit(readOnlySpan[2]) || !char.IsDigit(readOnlySpan[3]) || !char.IsDigit(readOnlySpan[4]) || !char.IsDigit(readOnlySpan[5]) || !char.IsDigit(readOnlySpan[6]) || !char.IsDigit(readOnlySpan[7]) || readOnlySpan[8] != '-' || !char.IsDigit(readOnlySpan[9]) || !char.IsDigit(readOnlySpan[10]) || !char.IsDigit(readOnlySpan[11]) || !char.IsDigit(readOnlySpan[12]) || !char.IsDigit(readOnlySpan[13]) || !char.IsDigit(readOnlySpan[14]))
				{
					UncaptureUntil(0);
					return false;
				}
				num += 15;
				readOnlySpan = inputSpan.Slice(num);
				Capture(1, num2, num);
				num3 = 0;
				while (true)
				{
					_003CRegexGenerator_g_003EFC2F671FD3A111B0AC8B64BAC66ABD7A103065EAF2FDEC5695C23E559BA17C004__Utilities.StackPush(ref runstack, ref pos, Crawlpos(), num);
					num3++;
					if (!readOnlySpan.IsEmpty && readOnlySpan[0] == '-')
					{
						num++;
						readOnlySpan = inputSpan.Slice(num);
						int start2 = num;
						int i;
						for (i = 0; i < 6 && (uint)i < (uint)readOnlySpan.Length && char.IsDigit(readOnlySpan[i]); i++)
						{
						}
						if (i != 0)
						{
							readOnlySpan = readOnlySpan.Slice(i);
							num += i;
							Capture(2, start2, num);
							if (num3 == 0)
							{
								continue;
							}
							goto IL_0230;
						}
					}
					goto IL_01f2;
					IL_0230:
					if (num >= inputSpan.Length - 1 && ((uint)num >= (uint)inputSpan.Length || inputSpan[num] == '\n'))
					{
						break;
					}
					goto IL_01f2;
					IL_01f2:
					if (--num3 < 0)
					{
						UncaptureUntil(0);
						return false;
					}
					num = runstack[--pos];
					UncaptureUntil(runstack[--pos]);
					readOnlySpan = inputSpan.Slice(num);
					goto IL_0230;
				}
				runtextpos = num;
				Capture(0, start, num);
				return true;
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				void UncaptureUntil(int capturePosition)
				{
					while (Crawlpos() > capturePosition)
					{
						Uncapture();
					}
				}
			}
		}

		protected override RegexRunner CreateInstance()
		{
			return new Runner();
		}
	}

	internal static readonly _003CRegexGenerator_g_003EFC2F671FD3A111B0AC8B64BAC66ABD7A103065EAF2FDEC5695C23E559BA17C004__SessionName_0 Instance = new _003CRegexGenerator_g_003EFC2F671FD3A111B0AC8B64BAC66ABD7A103065EAF2FDEC5695C23E559BA17C004__SessionName_0();

	private _003CRegexGenerator_g_003EFC2F671FD3A111B0AC8B64BAC66ABD7A103065EAF2FDEC5695C23E559BA17C004__SessionName_0()
	{
		pattern = "^(\\d{8}-\\d{6})(?:-(\\d{1,6}))?$";
		roptions = RegexOptions.None;
		Regex.ValidateMatchTimeout(_003CRegexGenerator_g_003EFC2F671FD3A111B0AC8B64BAC66ABD7A103065EAF2FDEC5695C23E559BA17C004__Utilities.s_defaultTimeout);
		internalMatchTimeout = _003CRegexGenerator_g_003EFC2F671FD3A111B0AC8B64BAC66ABD7A103065EAF2FDEC5695C23E559BA17C004__Utilities.s_defaultTimeout;
		factory = new RunnerFactory();
		capsize = 3;
	}
}
