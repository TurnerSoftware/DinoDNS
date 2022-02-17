using System.Net;

namespace TurnerSoftware.DinoDNS.Protocol.ResourceRecords;

public readonly record struct ARecord
{
	public LabelSequence DomainName { get; init; }
	public DnsClass Class { get; init; }
	public uint TimeToLive { get; init; }


	private readonly ReadOnlyMemory<byte> _Data;
	private readonly IPAddress? _IPAddress;
	public IPAddress IPAddress
	{
		get => _IPAddress ?? new(_Data.Span);
		init => _IPAddress = value;
	}

	public ARecord(ResourceRecord record)
	{
		DomainName = record.DomainName;
		Class = record.Class;
		TimeToLive = record.TimeToLive;
		_Data = record.Data;
		_IPAddress = default;
	}

	public static implicit operator ResourceRecord(ARecord record)
	{
		var data = record._IPAddress?.GetAddressBytes().AsMemory() ?? record._Data;
		return new ResourceRecord(
			record.DomainName,
			DnsType.A,
			record.Class,
			record.TimeToLive,
			(ushort)data.Length,
			data
		);
	}
}
