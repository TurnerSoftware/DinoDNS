using System.Buffers.Binary;
using System.Text;

namespace TurnerSoftware.DinoDNS.Protocol;

public ref partial struct LabelSequence
{
	public ref struct LabelSequenceReader
	{
		private SeekableReadOnlySpan<byte> Value;

		public bool EndOfSequence { get; private set; }

		public LabelSequenceReader(SeekableReadOnlySpan<byte> value)
		{
			Value = value;
			EndOfSequence = false;
		}

		/// <summary>
		/// Something is wrong here - I should set up some basics tests on label reading
		/// </summary>
		/// <param name="label"></param>
		/// <param name="fromPointer"></param>
		/// <returns></returns>
		public bool Next(out ReadOnlySpan<byte> label, out bool fromPointer)
		{
			if (EndOfSequence)
			{
				label = default;
				fromPointer = false;
				return false;
			}

			var isPointerReference = false;

			NextLabel:
			var countOrPointer = Value.Current;
			if (countOrPointer > 0)
			{
				if (countOrPointer < 63)
				{
					Value = Value.SeekRelative(1).ReadNext(countOrPointer, out label);
					fromPointer = isPointerReference;
					return true;
				}
				else if ((countOrPointer & PointerFlagByte) == PointerFlagByte)
				{
					//Pointers are a part of DNS message compression.
					//The first two bits say whether it is a pointer or not.
					//The next 14 bits represent the offset from the beginning of the message.
					var offset = BinaryPrimitives.ReadUInt16BigEndian(Value) & 0b00111111_11111111;
					Value = Value.Seek(offset);
					isPointerReference = true;
					goto NextLabel;
				}
			}

			label = default;
			fromPointer = false;
			return false;
		}

		public string ReadString()
		{
			var builder = new StringBuilder();
			var isNotFirst = false;
			while (Next(out var label, out _))
			{
				if (isNotFirst)
				{
					builder.Append('.');
				}

				var encodedLabel = Encoding.ASCII.GetString(label);
				builder.Append(encodedLabel);
				isNotFirst = true;
			}
			return builder.ToString();
		}

		public void CountBytes(out int sequentialBytes, out int sequenceBytes)
		{
			sequentialBytes = 0;

			var hasPointer = false;
			var pointedLabelBytes = 0;
			while (Next(out var label, out var fromPointer))
			{
				//The "+1" is because each label starts with a number
				var labelBytes = label.Length + 1;

				if (!fromPointer)
				{
					sequentialBytes += labelBytes;
					continue;
				}

				hasPointer = true;
				pointedLabelBytes = labelBytes;
				break;
			}

			if (!hasPointer)
			{
				//Non-pointer sequences have a sequential 0-value byte at the end.
				sequentialBytes += 1;
				sequenceBytes = sequentialBytes;
				return;
			}

			sequenceBytes = sequentialBytes + pointedLabelBytes;
			sequentialBytes += PointerLength;

			while (Next(out var label, out _))
			{
				//The "+1" is because each label starts with a number
				var labelBytes = label.Length + 1;
				sequenceBytes += labelBytes;
			}

			//As we have had a pointer, we track the 0-byte value in the sequence bytes only.
			sequenceBytes += 1;
		}
	}
}
