using System.Buffers.Binary;

namespace TurnerSoftware.DinoDNS.Protocol;

public readonly record struct Question(LabelSequence Query, DnsQueryType Type, DnsClass Class)
{
	public static Question Parse(SeekableMemory<byte> source, out int bytesRead)
	{
		source
			.ReadLabelSequence(out var labelSequence, out bytesRead)
			.ReadUInt16BigEndian(out var type)
			.ReadUInt16BigEndian(out var dnsClass);

		bytesRead += 4;

		return new Question(labelSequence, (DnsQueryType)type, (DnsClass)dnsClass);
	}

	public override string ToString() => $"QNAME:{Query},QTYPE:{Type},QCLASS:{Class}";
}