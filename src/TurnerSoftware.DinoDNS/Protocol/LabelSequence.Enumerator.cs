using System.Buffers.Binary;
using System.Collections;

namespace TurnerSoftware.DinoDNS.Protocol;

public readonly partial struct LabelSequence
{
	public const ushort PointerFlagByte = 0b11000000;
	public const byte PointerLength = sizeof(ushort);

	public struct Enumerator : IEnumerator<Label>
	{
		private readonly LabelSequence Value;
		private Label CurrentLabel;
		private int Index;

		public readonly Label Current => CurrentLabel;
		readonly object IEnumerator.Current => CurrentLabel;

		internal Enumerator(LabelSequence value)
		{
			Value = value;
			Index = value.IsByteSequence ? value.ByteValue.Offset : 0;
			CurrentLabel = default;
		}

		private bool NextCharLabel()
		{
			var indexSlice = Value.CharValue[Index..];
			if (indexSlice.IsEmpty)
			{
				CurrentLabel = default;
				return false;
			}

			var nextIndex = indexSlice.Span.IndexOf('.');
			var foundSeparator = nextIndex != -1;
			if (!foundSeparator)
			{
				nextIndex = indexSlice.Length;
			}

			var value = indexSlice[..nextIndex];
			CurrentLabel = new Label(value);
			Index += (foundSeparator ? nextIndex + 1 : nextIndex);
			return true;
		}

		private bool NextByteLabel()
		{
			var seekableMemory = Value.ByteValue.Seek(Index);
			var fromPointer = false;

			var countOrPointer = seekableMemory.Current;
			while ((countOrPointer & PointerFlagByte) == PointerFlagByte)
			{
				//Pointers are a part of DNS message compression.
				//The first two bits say whether it is a pointer or not.
				//The next 14 bits represent the offset from the beginning of the message.
				var offset = BinaryPrimitives.ReadUInt16BigEndian(seekableMemory) & 0b00111111_11111111;
				seekableMemory = seekableMemory.Seek(offset);
				countOrPointer = seekableMemory.Current;
				fromPointer = true;
			}

			if (countOrPointer > 0 && countOrPointer <= Label.MaxLength)
			{
				seekableMemory = seekableMemory.ReadNext(countOrPointer + 1, out var value);
				CurrentLabel = new Label(value[1..], fromPointer);
				Index = seekableMemory.Offset;
				return true;
			}

			CurrentLabel = default;
			return false;
		}

		public bool MoveNext()
		{
			if (Value.IsByteSequence)
			{
				return NextByteLabel();
			}
			return NextCharLabel();
		}

		public void Reset()
		{
			Index = 0;
			CurrentLabel = default;
		}

		public readonly void Dispose() { }
	}
}