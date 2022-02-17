namespace TurnerSoftware.DinoDNS.Protocol;

/// <summary>
/// 
/// </summary>
/// <param name="DomainName"></param>
/// <param name="Type"></param>
/// <param name="Class"></param>
/// <param name="TimeToLive">Number of seconds the resource can be cached for.</param>
/// <param name="ResourceDataLength"></param>
/// <param name="Data"></param>
public readonly record struct ResourceRecord(
	LabelSequence DomainName,
	DnsType Type,
	DnsClass Class,
	uint TimeToLive,
	ushort ResourceDataLength,
	ReadOnlyMemory<byte> Data
)
{
	public override string ToString() => $"DomainName:{DomainName.ToString()},Type:{Type},CLASS:{Class},TTL:{TimeToLive},Length:{ResourceDataLength}";
}
