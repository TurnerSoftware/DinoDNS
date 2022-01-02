using System.Buffers.Binary;

namespace TurnerSoftware.DinoDNS.Protocol;

public ref struct Question
{
	public readonly LabelSequence Query;
	public readonly DnsQueryType Type;
	public readonly DnsClass Class;

	public Question(LabelSequence labelSequence, DnsQueryType dnsQueryType, DnsClass dnsClass)
	{
		Query = labelSequence;
		Type = dnsQueryType;
		Class = dnsClass;
	}

	public static Question Parse(SeekableReadOnlySpan<byte> source, out int bytesRead)
	{
		source
			.ReadLabelSequence(out var labelSequence, out bytesRead)
			.ReadUInt16BigEndian(out var type)
			.ReadUInt16BigEndian(out var dnsClass);

		bytesRead += 4;

		return new Question(labelSequence, (DnsQueryType)type, (DnsClass)dnsClass);
	}

	public void WriteTo(Span<byte> destination, out int bytesWritten)
	{
		Query.WriteTo(destination, out bytesWritten);
		destination = destination[bytesWritten..];
		bytesWritten += 4;

		BinaryPrimitives.WriteUInt16BigEndian(destination, (ushort)Type);
		BinaryPrimitives.WriteUInt16BigEndian(destination[2..], (ushort)Class);
	}

	public override string ToString() => $"QNAME:{Query.ToString()},QTYPE:{Type},QCLASS:{Class}";
}