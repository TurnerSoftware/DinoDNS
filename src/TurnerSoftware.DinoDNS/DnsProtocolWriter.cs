using System.Buffers.Binary;
using TurnerSoftware.DinoDNS.Protocol;

namespace TurnerSoftware.DinoDNS;

public readonly struct DnsProtocolWriter
{
	public readonly SeekableMemory<byte> SeekableDestination;

	public DnsProtocolWriter(SeekableMemory<byte> destination)
	{
		SeekableDestination = destination;
	}

	private DnsProtocolWriter Advance(int bytesWritten)
	{
		return new(SeekableDestination.SeekRelative(bytesWritten));
	}

	public DnsProtocolWriter AppendByte(byte value)
	{
		SeekableDestination.Span[0] = value;
		return Advance(sizeof(byte));
	}
	public DnsProtocolWriter AppendUInt16(ushort value)
	{
		BinaryPrimitives.WriteUInt16BigEndian(SeekableDestination.Span, value);
		return Advance(sizeof(ushort));
	}
	public DnsProtocolWriter AppendUInt32(uint value)
	{
		BinaryPrimitives.WriteUInt32BigEndian(SeekableDestination.Span, value);
		return Advance(sizeof(uint));
	}
	public DnsProtocolWriter AppendBytes(ReadOnlySpan<byte> value)
	{
		value.CopyTo(SeekableDestination.Span);
		return Advance(value.Length);
	}

	public DnsProtocolWriter AppendHeader(Header header)
	{
		return AppendUInt16(header.Identification)
			.AppendUInt16(header.Flags.Value)
			.AppendUInt16(header.QuestionRecordCount)
			.AppendUInt16(header.AnswerRecordCount)
			.AppendUInt16(header.AuthorityRecordCount)
			.AppendUInt16(header.AdditionalRecordCount);
	}

	public DnsProtocolWriter AppendQuestion(Question question)
	{
		return AppendLabelSequence(question.Query)
			.AppendUInt16((ushort)question.Type)
			.AppendUInt16((ushort)question.Class);
	}

	public DnsProtocolWriter AppendPointer(ushort offset)
	{
		//Set the first 2-bits of the value to correctly encode the pointer before writing.
		offset |= 0b11000000_00000000;
		return AppendUInt16(offset);
	}

	public DnsProtocolWriter AppendLabel(LabelSequence.Label label)
	{
		Span<byte> buffer = stackalloc byte[LabelSequence.Label.MaxLength];
		var bytesEncoded = label.ToBytes(buffer);
		return AppendByte((byte)bytesEncoded)
			.AppendBytes(buffer[..bytesEncoded]);
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
	public DnsProtocolWriter AppendLabelSequenceEnd() => AppendByte(0);

	public DnsProtocolWriter AppendResourceRecord(ResourceRecord resourceRecord)
	{
		return AppendLabelSequence(resourceRecord.DomainName)
			.AppendUInt16((ushort)resourceRecord.Type)
			.AppendUInt16((ushort)resourceRecord.Class)
			.AppendUInt32(resourceRecord.TimeToLive)
			.AppendUInt16(resourceRecord.ResourceDataLength)
			.AppendBytes(resourceRecord.Data.Span);
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

	public ReadOnlyMemory<byte> GetWrittenBytes() => SeekableDestination.Source[..SeekableDestination.Offset];
}
