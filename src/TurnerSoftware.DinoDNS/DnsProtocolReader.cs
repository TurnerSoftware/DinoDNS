using System.Buffers.Binary;
using TurnerSoftware.DinoDNS.Protocol;

namespace TurnerSoftware.DinoDNS;

public readonly struct DnsProtocolReader
{
	public readonly SeekableReadOnlyMemory<byte> SeekableSource;

	public DnsProtocolReader(SeekableReadOnlyMemory<byte> seekableSource)
	{
		SeekableSource = seekableSource;
	}

	private DnsProtocolReader Advance(int bytesRead)
	{
		return new(SeekableSource.SeekRelative(bytesRead));
	}

	public DnsProtocolReader ReadUInt16(out ushort value)
	{
		value = BinaryPrimitives.ReadUInt16BigEndian(SeekableSource.Span);
		return Advance(sizeof(ushort));
	}
	public DnsProtocolReader ReadUInt32(out uint value)
	{
		value = BinaryPrimitives.ReadUInt32BigEndian(SeekableSource.Span);
		return Advance(sizeof(uint));
	}
	public DnsProtocolReader ReadNext(int bytes, out ReadOnlyMemory<byte> value)
	{
		value = SeekableSource.Memory[..bytes];
		return Advance(bytes);
	}

	public DnsProtocolReader ReadHeader(out Header header)
	{
		var reader = ReadUInt16(out var identification)
			.ReadUInt16(out var headerFlags)
			.ReadUInt16(out var questionRecordCount)
			.ReadUInt16(out var answerRecordCount)
			.ReadUInt16(out var authorityRecordCount)
			.ReadUInt16(out var additionalRecordCount);

		header = new Header(
			identification,
			new HeaderFlags(headerFlags),
			questionRecordCount,
			answerRecordCount,
			authorityRecordCount,
			additionalRecordCount
		);

		return reader;
	}

	public DnsProtocolReader ReadQuestion(out Question question)
	{
		var reader = ReadLabelSequence(out var query)
			.ReadUInt16(out var type)
			.ReadUInt16(out var dnsClass);

		question = new Question(
			query,
			(DnsQueryType)type,
			(DnsClass)dnsClass
		);

		return reader;
	}

	public DnsProtocolReader ReadQuestionCollection(out QuestionCollection questions, int itemCount)
	{
		var reader = this;
		questions = new QuestionCollection(SeekableSource, itemCount);

		//Move the reader ahead by an equal number of questions
		for (var i = 0; i < itemCount; i++)
		{
			reader = reader.ReadQuestion(out _);
		}
		return reader;
	}

	public DnsProtocolReader ReadLabelSequence(out LabelSequence labelSequence)
	{
		labelSequence = new LabelSequence(SeekableSource);
		return Advance(labelSequence.GetSequentialByteLength());
	}

	public DnsProtocolReader ReadResourceRecord(out ResourceRecord resourceRecord)
	{
		var reader = ReadLabelSequence(out var domainName)
			.ReadUInt16(out var type)
			.ReadUInt16(out var dnsClass)
			.ReadUInt32(out var timeToLive)
			.ReadUInt16(out var resourceDataLength)
			.ReadNext(resourceDataLength, out var data);

		resourceRecord = new ResourceRecord(
			domainName,
			(DnsType)type,
			(DnsClass)dnsClass,
			timeToLive,
			resourceDataLength,
			data
		);

		return reader;
	}

	public DnsProtocolReader ReadResourceRecordCollection(out ResourceRecordCollection resourceRecords, int itemCount)
	{
		var reader = this;
		resourceRecords = new ResourceRecordCollection(SeekableSource, itemCount);

		//Move the reader ahead by an equal number of resource records
		for (var i = 0; i < itemCount; i++)
		{
			reader = reader.ReadResourceRecord(out _);
		}
		return reader;
	}

	/// <summary>
	/// This performs no message compression.
	/// </summary>
	/// <param name="message"></param>
	/// <returns></returns>
	public DnsProtocolReader ReadMessage(out DnsMessage message)
	{
		var reader = ReadHeader(out var header)
			.ReadQuestionCollection(out var questions, header.QuestionRecordCount)
			.ReadResourceRecordCollection(out var answers, header.AnswerRecordCount)
			.ReadResourceRecordCollection(out var authorities, header.AuthorityRecordCount)
			.ReadResourceRecordCollection(out var additionalRecords, header.AdditionalRecordCount);

		message = new DnsMessage(header, questions, answers, authorities, additionalRecords);
		return reader;
	}
}
