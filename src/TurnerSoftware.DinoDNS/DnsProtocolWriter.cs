using System.Buffers.Binary;
using TurnerSoftware.DinoDNS.Protocol;

namespace TurnerSoftware.DinoDNS;

public ref struct DnsProtocolWriter
{
	public readonly Memory<byte> Destination;
	public readonly int Offset;

	public DnsProtocolWriter(Memory<byte> destination)
	{
		Destination = destination;
		Offset = 0;
	}

	private DnsProtocolWriter(Memory<byte> destination, int offset)
	{
		Destination = destination;
		Offset = offset;
	}

	private DnsProtocolWriter Advance(int bytesWritten)
	{
		var newOffset = Offset + bytesWritten;
		return new(Destination, newOffset);
	}

	private Span<byte> OffsetDestination => Destination[Offset..].Span;

	public DnsProtocolWriter AppendHeader(Header header)
	{
		var destination = OffsetDestination;
		BinaryPrimitives.WriteUInt16BigEndian(destination[..2], header.Identification);
		BinaryPrimitives.WriteUInt16BigEndian(destination[2..4], header.Flags.Value);
		BinaryPrimitives.WriteUInt16BigEndian(destination[4..6], header.QuestionRecordCount);
		BinaryPrimitives.WriteUInt16BigEndian(destination[6..8], header.AnswerRecordCount);
		BinaryPrimitives.WriteUInt16BigEndian(destination[8..10], header.AuthorityRecordCount);
		BinaryPrimitives.WriteUInt16BigEndian(destination[10..12], header.AdditionalRecordCount);
		return Advance(Header.Length);
	}

	public DnsProtocolWriter AppendQuestion(Question question)
	{
		var writer = AppendLabelSequence(question.Query);
		var destination = writer.OffsetDestination;
		BinaryPrimitives.WriteUInt16BigEndian(destination, (ushort)question.Type);
		BinaryPrimitives.WriteUInt16BigEndian(destination[2..], (ushort)question.Class);
		return writer.Advance(4);
	}

	public DnsProtocolWriter AppendPointer(ushort pointer)
	{
		//Set the first 2-bits of the value to correctly encode the pointer before writing.
		pointer |= 0b11000000_00000000;
		var destination = OffsetDestination;
		BinaryPrimitives.WriteUInt16BigEndian(destination, pointer);
		return Advance(2);
	}

	public DnsProtocolWriter AppendLabel(LabelSequence.Label label)
	{
		var destination = OffsetDestination;
		var labelBytes = label.ToBytes();
		destination[0] = (byte)labelBytes.Length;
		labelBytes.CopyTo(destination[1..]);
		return Advance(labelBytes.Length + 1);
	}

	/// <summary>
	/// Writes an uncompressed label sequence including the ending NUL value.
	/// </summary>
	/// <param name="labelSequence"></param>
	/// <returns></returns>
	public DnsProtocolWriter AppendLabelSequence(LabelSequence labelSequence)
	{
		var writer = this;
		foreach (var label in labelSequence)
		{
			writer = writer.AppendLabel(label);
		}
		return writer.AppendLabelSequenceEnd();
	}

	/// <summary>
	/// Writes the NUL value, ending a label sequence.
	/// This is automatically called from <see cref="WriteLabelSequence(LabelSequence)"/>.
	/// </summary>
	/// <returns></returns>
	public DnsProtocolWriter AppendLabelSequenceEnd()
	{
		OffsetDestination[0] = 0;
		return Advance(1);
	}

	public DnsProtocolWriter AppendResourceRecord(ResourceRecord resourceRecord)
	{
		var writer = AppendLabelSequence(resourceRecord.DomainName);
		var destination = writer.OffsetDestination;
		BinaryPrimitives.WriteUInt16BigEndian(destination, (ushort)resourceRecord.Type);
		BinaryPrimitives.WriteUInt16BigEndian(destination[2..], (ushort)resourceRecord.Class);
		BinaryPrimitives.WriteUInt32BigEndian(destination[4..], resourceRecord.TimeToLive);
		BinaryPrimitives.WriteUInt16BigEndian(destination[8..], resourceRecord.ResourceDataLength);
		var bytesWritten = 10;
		resourceRecord.Data.Span.CopyTo(destination);
		bytesWritten += resourceRecord.Data.Length;
		return Advance(bytesWritten);
	}

	/// <summary>
	/// This performs no message compression.
	/// </summary>
	/// <param name="message"></param>
	/// <returns></returns>
	public DnsProtocolWriter AppendMessage(DnsMessage message)
	{
		var writer = AppendHeader(message.Header);

		if (message.Questions is not null)
		{
			foreach (var question in message.Questions)
			{
				writer = writer.AppendQuestion(question);
			}
		}

		if (message.Answers is not null)
		{
			foreach (var answer in message.Answers)
			{
				writer = writer.AppendResourceRecord(answer);
			}
		}

		if (message.Authorities is not null)
		{
			foreach (var authority in message.Authorities)
			{
				writer = writer.AppendResourceRecord(authority);
			}
		}

		if (message.AdditionalRecords is not null)
		{
			foreach (var additionalRecord in message.AdditionalRecords)
			{
				writer = writer.AppendResourceRecord(additionalRecord);
			}
		}

		return writer;
	}

	public ReadOnlyMemory<byte> GetWrittenBytes() => Destination[..Offset];
}
