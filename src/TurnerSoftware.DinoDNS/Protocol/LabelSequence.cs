using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using TurnerSoftware.DinoDNS.Internal;

namespace TurnerSoftware.DinoDNS.Protocol;

public readonly partial struct LabelSequence : IEquatable<LabelSequence>
{
	private readonly SeekableReadOnlyMemory<byte> ByteValue;
	private readonly ReadOnlyMemory<char> CharValue;

	public LabelSequence(SeekableReadOnlyMemory<byte> value)
	{
		ByteValue = value;
		CharValue = ReadOnlyMemory<char>.Empty;
	}
	public LabelSequence(ReadOnlyMemory<char> value)
	{
		ByteValue = default;
		CharValue = value;
	}
	public LabelSequence(string value) : this(value.AsMemory()) { }
	public LabelSequence(DnsRawValue value)
	{
		ByteValue = value.ByteValue;
		CharValue = value.CharValue;
	}

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

	public unsafe int GetSequenceByteLength()
	{
		var sequenceLength = 0;

		if (Avx2.IsSupported && CharValue.IsEmpty)
		{
			var labelPointer = Vector256.Create((byte)(PointerFlagByte - 1));
			var endOfLabel = Vector256<byte>.Zero;

			fixed (byte* fixedBytePtr = ByteValue.Source.Span)
			{
				var index = ByteValue.Offset;
				var length = ByteValue.Source.Length;
				while (index < length)
				{
					var bytePtr = fixedBytePtr + index;
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
						sequenceLength += Vector256<byte>.Count;
					}
					else if (endLabelIndex < labelPointerIndex)
					{
						sequenceLength += endLabelIndex + 1;
						break;
					}
					else
					{
						var labelPointerSpan = new Span<byte>(bytePtr + labelPointerIndex, 2);
						index = BinaryPrimitives.ReadUInt16BigEndian(labelPointerSpan) & PointerOffsetBits;
						sequenceLength += labelPointerIndex;
					}
				}
			}
		}
		else
		{
			foreach (var label in this)
			{
				//The "+1" is because each label starts with a number
				sequenceLength += label.Length + 1;
			}
			//Add the final 0-length label byte
			sequenceLength += 1;
		}

		return sequenceLength;
	}

	public bool Equals(LabelSequence other)
	{
		var enumerator = GetEnumerator();
		var otherEnumerator = other.GetEnumerator();

		while (enumerator.MoveNext() && otherEnumerator.MoveNext())
		{
			if (!enumerator.Current.Equals(otherEnumerator.Current))
			{
				return false;
			}
		}

		return true;
	}

	public override bool Equals([NotNullWhen(true)] object? obj) => obj is LabelSequence value && Equals(value);

	public override int GetHashCode() => HashCode.Combine(ByteValue, CharValue);

	/// <summary>
	/// Attempts to write the label sequence, in dot-format with trailing dot, to the destination span.
	/// </summary>
	/// <remarks>
	/// Example: <c>www.example.org.</c>
	/// </remarks>
	/// <param name="destination"></param>
	/// <param name="bytesWritten"></param>
	/// <returns></returns>
	public bool TryWriteUnencodedBytes(Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;
		if (destination.Length < GetSequenceByteLength())
		{
			return false;
		}

		var offset = 0;
		foreach (var label in this)
		{
			label.Value.TryWriteBytes(destination[offset..], out var localBytesWritten);
			offset += localBytesWritten;
			destination[offset] = (byte)'.';
			offset++;
		}
		bytesWritten = offset;
		return true;
	}

	/// <summary>
	/// Attempts to write the label sequence, encoded for the wire protocol with trailing "0" byte, to the destination span.
	/// </summary>
	/// <remarks>
	/// Example: <c>3www7example3org0</c>
	/// </remarks>
	/// <param name="destination"></param>
	/// <param name="bytesWritten"></param>
	/// <returns></returns>
	public bool TryWriteEncodedBytes(Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;
		if (destination.Length < GetSequenceByteLength())
		{
			return false;
		}

		var offset = 0;
		foreach (var label in this)
		{
			var segment = destination[offset..];
			segment[0] = (byte)label.Length;
			label.Value.TryWriteBytes(segment[1..], out var localBytesWritten);
			offset += localBytesWritten + 1;
		}

		destination[offset] = 0;
		bytesWritten = offset + 1;
		return true;
	}

	public override string ToString()
	{
		if (!CharValue.IsEmpty)
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
	public static implicit operator LabelSequence(DnsRawValue value) => new(value);

	public static bool operator ==(in LabelSequence left, in LabelSequence right) => left.Equals(right);
	public static bool operator !=(in LabelSequence left, in LabelSequence right) => !(left == right);
}
