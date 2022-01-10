using System.Text;

namespace TurnerSoftware.DinoDNS.Protocol;

public readonly partial struct LabelSequence
{
	public readonly struct Label
	{
		public const int MaxLength = 63;

		public readonly ReadOnlyMemory<byte> ByteValue;
		public readonly ReadOnlyMemory<char> CharValue;
		public readonly bool FromPointerOffset;

		public Label(ReadOnlyMemory<byte> value, bool fromPointerOffset)
		{
			if (value.Length > MaxLength)
			{
				throw new ArgumentException($"The max length of a label is {MaxLength} characters.");
			}

			ByteValue = value;
			CharValue = default;
			FromPointerOffset = fromPointerOffset;
		}

		public Label(ReadOnlyMemory<char> value)
		{
			if (value.Length > MaxLength)
			{
				throw new ArgumentException($"The max length of a label is {MaxLength} characters.");
			}

			ByteValue = default;
			CharValue = value;
			FromPointerOffset = false;
		}

		public bool IsEmpty => ByteValue.IsEmpty && CharValue.IsEmpty;

		public int Length => !ByteValue.IsEmpty ? ByteValue.Length : CharValue.Length;

		public bool Equals(ReadOnlySpan<char> other)
		{
			if (!ByteValue.IsEmpty)
			{
				Span<byte> buffer = stackalloc byte[63];
				var asciiBytes = Encoding.ASCII.GetBytes(other, buffer);
				return ByteValue.Span.SequenceEqual(buffer[..asciiBytes]);
			}

			return CharValue.Span.SequenceEqual(other);
		}

		public bool Equals(ReadOnlySpan<byte> other)
		{
			if (!CharValue.IsEmpty)
			{
				Span<byte> buffer = stackalloc byte[63];
				var asciiBytes = Encoding.ASCII.GetBytes(CharValue.Span, buffer);
				return buffer[..asciiBytes].SequenceEqual(other);
			}

			return ByteValue.Span.SequenceEqual(other);
		}

		public bool Equals(Label other)
		{
			if (!other.ByteValue.IsEmpty)
			{
				return Equals(other.ByteValue);
			}
			else if (!other.CharValue.IsEmpty)
			{
				return Equals(other.CharValue);
			}
			return Length == 0;
		}

		public ReadOnlySpan<byte> ToBytes()
		{
			if (!ByteValue.IsEmpty)
			{
				return ByteValue.Span;
			}

			var byteCount = Encoding.ASCII.GetByteCount(CharValue.Span);
			var result = new byte[byteCount];
			Encoding.ASCII.GetBytes(CharValue.Span, result);
			return result;
		}

		public int ToBytes(Span<byte> destination)
		{
			if (!ByteValue.IsEmpty)
			{
				ByteValue.Span.CopyTo(destination);
				return ByteValue.Length;
			}

			return Encoding.ASCII.GetBytes(CharValue.Span, destination);
		}

		public override string ToString()
		{
			if (!CharValue.IsEmpty)
			{
				return CharValue.ToString();
			}
			else if (!ByteValue.IsEmpty)
			{
				return Encoding.ASCII.GetString(ByteValue.Span);
			}

			return string.Empty;
		}

		public int ToString(Span<char> destination)
		{
			if (!CharValue.IsEmpty)
			{
				CharValue.Span.CopyTo(destination);
				return CharValue.Length;
			}

			return Encoding.ASCII.GetChars(ByteValue.Span, destination);
		}

		public static implicit operator Label(string source) => new(source.AsMemory());
		public static implicit operator Label(ReadOnlyMemory<char> source) => new(source);
	}
}
