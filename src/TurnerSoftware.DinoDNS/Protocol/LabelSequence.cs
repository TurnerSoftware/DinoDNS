using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace TurnerSoftware.DinoDNS.Protocol;

public readonly partial struct LabelSequence
{
	private readonly SeekableReadOnlyMemory<byte> ByteValue;
	private readonly ReadOnlyMemory<char> CharValue;
	private readonly bool IsByteSequence;

	public LabelSequence(SeekableReadOnlyMemory<byte> value)
	{
		ByteValue = value;
		CharValue = ReadOnlyMemory<char>.Empty;
		IsByteSequence = true;
	}
	public LabelSequence(ReadOnlyMemory<char> value)
	{
		ByteValue = default;
		CharValue = value;
		IsByteSequence = false;
	}
	public LabelSequence(string value) : this(value.AsMemory()) { }

	public Enumerator GetEnumerator() => new(this);

	public unsafe int GetSequentialByteLength()
	{
		var sequentialBytes = 0;

		if (Avx2.IsSupported)
		{
			var labelPointer = Vector256.Create((byte)(PointerFlagByte - 1));
			var endOfLabel = Vector256<byte>.Zero;

			fixed (byte* fixedBytePtr = ByteValue.Span)
			{
				var index = 0;
				while (index < ByteValue.Span.Length)
				{
					var bytePtr = fixedBytePtr;
					var data = Avx.LoadVector256(bytePtr);
					var endLabelIndex = BitOperations.TrailingZeroCount(
						(uint)Avx2.MoveMask(
							Avx2.CompareEqual(endOfLabel, data)
						)
					);
					var labelPointerIndex = BitOperations.TrailingZeroCount(
						(uint)Avx2.MoveMask(
							Avx2.CompareEqual(
								labelPointer,
								Avx2.Max(labelPointer, data)
							)
						) ^ uint.MaxValue
					);

					if (endLabelIndex == Vector256<byte>.Count && labelPointerIndex == Vector256<byte>.Count)
					{
						index += Vector256<byte>.Count;
						sequentialBytes += Vector256<byte>.Count;
					}
					else if (endLabelIndex < labelPointerIndex)
					{
						sequentialBytes += endLabelIndex + 1;
						break;
					}
					else
					{
						sequentialBytes += labelPointerIndex + PointerLength;
						break;
					}
				}
			}
		}
		else
		{
			var hasPointer = false;
			foreach (var label in this)
			{
				if (!label.FromPointerOffset)
				{
					//The "+1" is because each label starts with a number that dictates the length
					sequentialBytes += label.Length + 1;
					continue;
				}

				sequentialBytes += PointerLength;
				hasPointer = true;
				break;
			}

			if (!hasPointer)
			{
				//Add the final 0-length label byte - only for non-pointers
				sequentialBytes += 1;
			}
		}

		return sequentialBytes;
	}

	public int GetSequenceByteLength()
	{
		var sequenceLength = 0;
		foreach (var label in this)
		{
			//The "+1" is because each label starts with a number
			sequenceLength += label.Length + 1;
		}
		//Add the final 0-length label byte
		sequenceLength += 1;
		return sequenceLength;
	}

	public override string ToString()
	{
		if (!IsByteSequence)
		{
			return CharValue.ToString();
		}
		else
		{
			var builder = new StringBuilder();
			var isNotFirst = false;
			foreach (var label in this)
			{
				if (isNotFirst)
				{
					builder.Append('.');
				}

				builder.Append(label.ToString());
				isNotFirst = true;
			}
			return builder.ToString();
		}
	}

	public static implicit operator LabelSequence(string source) => new(source.AsMemory());
}
