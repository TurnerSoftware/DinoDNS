namespace TurnerSoftware.DinoDNS.Protocol;

public ref struct ResourceRecord
{
	public readonly LabelSequence DomainName;
	public readonly DnsType Type;
	public readonly DnsClass Class;
	/// <summary>
	/// Number of seconds the resource record can be cached.
	/// </summary>
	public readonly uint TimeToLive;
	public readonly ushort ResourceDataLength;

	public readonly ReadOnlySpan<byte> Data;

	public ResourceRecord(LabelSequence labelSequence, DnsType dnsType, DnsClass dnsClass, uint timeToLive)
	{
		DomainName = labelSequence;
		Type = dnsType;
		Class = dnsClass;
		TimeToLive = timeToLive;
		ResourceDataLength = 0;
		Data = ReadOnlySpan<byte>.Empty;
	}

	public ResourceRecord(LabelSequence labelSequence, DnsType dnsType, DnsClass dnsClass, uint timeToLive, ReadOnlySpan<byte> data)
	{
		DomainName = labelSequence;
		Type = dnsType;
		Class = dnsClass;
		TimeToLive = timeToLive;
		ResourceDataLength = (ushort)data.Length;
		Data = data;
	}

	public static ResourceRecord Parse(SeekableReadOnlySpan<byte> source, out int bytesRead)
	{
		source
			.ReadLabelSequence(out var labelSequence, out bytesRead)
			.ReadUInt16BigEndian(out var type)
			.ReadUInt16BigEndian(out var dnsClass)
			.ReadUInt32BigEndian(out var timeToLive)
			.ReadUInt16BigEndian(out var resourceDataLength)
			.ReadNext(resourceDataLength, out var data);
		
		bytesRead += 10;
		bytesRead += resourceDataLength;

		return new ResourceRecord(labelSequence, (DnsType)type, (DnsClass)dnsClass, timeToLive, data);
	}

	public override string ToString() => $"DomainName:{DomainName.ToString()},Type:{Type},CLASS:{Class},TTL:{TimeToLive},Length:{ResourceDataLength}";
}
