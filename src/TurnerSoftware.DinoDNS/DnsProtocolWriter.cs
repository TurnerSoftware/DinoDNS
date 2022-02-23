﻿using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using TurnerSoftware.DinoDNS.Internal;
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

	public unsafe DnsProtocolWriter AppendHeader(Header header)
	{
		if (SeekableDestination.Remaining < Header.Length)
		{
			throw new InvalidOperationException("Not enough space to append header");
		}

		if (Ssse3.IsSupported)
		{
			fixed (byte* fixedBytePtr = SeekableDestination.Span)
			{
				var headerVector = Unsafe.As<Header, Vector128<byte>>(ref header);
				
				//Overwrite the extra 4-byte data we retrieved unsafely with zeroes
				headerVector = headerVector.AsInt32().WithElement(3, 0).AsByte();

				if (BitConverter.IsLittleEndian)
				{
					headerVector = Ssse3.Shuffle(headerVector, Header.EndianShuffle);
				}

				Sse2.Store(fixedBytePtr, headerVector);
				return Advance(Header.Length);
			}
		}
		else
		{
			return AppendUInt16(header.Identification)
				.AppendUInt16(header.Flags.Value)
				.AppendUInt16(header.QuestionRecordCount)
				.AppendUInt16(header.AnswerRecordCount)
				.AppendUInt16(header.AuthorityRecordCount)
				.AppendUInt16(header.AdditionalRecordCount);
		}
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

	public unsafe DnsProtocolWriter AppendLabel(LabelSequence.Label label)
	{
		var bytesWritten = (byte)label.ToBytes(
			SeekableDestination.Span[1..]
		);
		SeekableDestination.Span[0] = bytesWritten;
		return Advance(1 + bytesWritten);
	}

	/// <summary>
	/// Writes an uncompressed label sequence including the ending NUL value.
	/// </summary>
	/// <param name="labelSequence"></param>
	/// <returns></returns>
	public DnsProtocolWriter AppendLabelSequence(LabelSequence labelSequence)
	{
		var writer = this;
		//We can't use the Label Sequence ByteValue directly because we don't know if it contains a pointer.
		//This is a problem as the pointer may not be intended to match the written data.
		foreach (var label in labelSequence)
		{
			writer = writer.AppendLabel(label);
		}
		return writer.AppendLabelSequenceEnd();
	}

	/// <summary>
	/// Writes the NUL value, ending a label sequence.
	/// This is automatically called from <see cref="AppendLabelSequence(LabelSequence)"/>.
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

		foreach (var question in message.Questions)
		{
			writer = writer.AppendQuestion(question);
		}

		foreach (var answer in message.Answers)
		{
			writer = writer.AppendResourceRecord(answer);
		}

		foreach (var authority in message.Authorities)
		{
			writer = writer.AppendResourceRecord(authority);
		}

		foreach (var additionalRecord in message.AdditionalRecords)
		{
			writer = writer.AppendResourceRecord(additionalRecord);
		}

		return writer;
	}

	public int BytesWritten => SeekableDestination.Offset;
	public ReadOnlyMemory<byte> GetWrittenBytes() => SeekableDestination.Source[..BytesWritten];
}
