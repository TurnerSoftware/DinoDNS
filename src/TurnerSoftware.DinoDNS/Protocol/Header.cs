using System.Buffers.Binary;

namespace TurnerSoftware.DinoDNS.Protocol;

/// <summary>
/// Represents the first 12-bytes of a DNS message
/// </summary>
public ref struct Header
{
	public const int Length = 12;

	public ushort Identification;
	public HeaderFlags Flags;
	public ushort QuestionRecordCount;
	public ushort AnswerRecordCount;
	public ushort AuthorityRecordCount;
	public ushort AdditionalRecordCount;

	public Header(ReadOnlySpan<byte> source)
	{
		Identification = BinaryPrimitives.ReadUInt16BigEndian(source[..2]);
		Flags = new HeaderFlags(source[2..4]);
		QuestionRecordCount = BinaryPrimitives.ReadUInt16BigEndian(source[4..6]);
		AnswerRecordCount = BinaryPrimitives.ReadUInt16BigEndian(source[6..8]);
		AuthorityRecordCount = BinaryPrimitives.ReadUInt16BigEndian(source[8..10]);
		AdditionalRecordCount = BinaryPrimitives.ReadUInt16BigEndian(source[10..12]);
	}

	public void WriteTo(Span<byte> destination)
	{
		BinaryPrimitives.WriteUInt16BigEndian(destination[..2], Identification);
		Flags.WriteTo(destination[2..4]);
		BinaryPrimitives.WriteUInt16BigEndian(destination[4..6], QuestionRecordCount);
		BinaryPrimitives.WriteUInt16BigEndian(destination[6..8], AnswerRecordCount);
		BinaryPrimitives.WriteUInt16BigEndian(destination[8..10], AuthorityRecordCount);
		BinaryPrimitives.WriteUInt16BigEndian(destination[10..12], AdditionalRecordCount);
	}

	public override string ToString()
	{
		var flags = Flags.ToString();
		return $"ID:{Identification:00000},{flags},QDCOUNT:{QuestionRecordCount},ANCOUNT:{AnswerRecordCount},NSCOUNT:{AuthorityRecordCount},ARCOUNT:{AdditionalRecordCount}";
	}
}

/// <summary>
/// Represents the 2-byte header flags of a DNS message
/// </summary>
public ref struct HeaderFlags
{
	private ushort Value;

	public QueryOrResponse QueryOrResponse
	{
		get => (QueryOrResponse)(Value & 0b10000000_00000000);
		set => Value = (ushort)(Value & ~0b10000000_00000000 | (int)value);
	}
	public Opcode Opcode
	{
		get => (Opcode)(Value & 0b01111000_00000000);
		set => Value = (ushort)(Value & ~0b01111000_00000000 | (int)value);
	}
	public AuthoritativeAnswer AuthoritativeAnswer
	{
		get => (AuthoritativeAnswer)(Value & 0b00000100_00000000);
		set => Value = (ushort)(Value & ~0b00000100_00000000 | (int)value);
	}
	public Truncation Truncation
	{
		get => (Truncation)(Value & 0b00000010_00000000);
		set => Value = (ushort)(Value & ~0b00000010_00000000 | (int)value);
	}
	public RecursionDesired RecursionDesired
	{
		get => (RecursionDesired)(Value & 0b00000001_00000000);
		set => Value = (ushort)(Value & ~0b00000001_00000000 | (int)value);
	}
	public RecursionAvailable RecursionAvailable
	{
		get => (RecursionAvailable)(Value & 0b00000000_10000000);
		set => Value = (ushort)(Value & ~0b00000000_10000000 | (int)value);
	}
	public ushort Z
	{
		get => (ushort)((Value & 0b00000000_01110000) >> 4);
		set => Value = (ushort)(Value & ~0b00000000_01110000 | value << 4);
	}
	public ResponseCode ResponseCode
	{
		get => (ResponseCode)(Value & 0b00000000_00001111);
		set => Value = (ushort)(Value & ~0b00000000_00001111 | (int)value);
	}

	public HeaderFlags(ReadOnlySpan<byte> source)
	{
		Value = BinaryPrimitives.ReadUInt16BigEndian(source);
	}

	public void WriteTo(Span<byte> destination)
	{
		BinaryPrimitives.WriteUInt16BigEndian(destination, Value);
	}

	public override string ToString() => $"[{Convert.ToString(Value,2).PadLeft(16,'0')}/{QueryOrResponse},{Opcode},{AuthoritativeAnswer},{Truncation},{RecursionDesired},{RecursionAvailable},{Z},{ResponseCode}]";
}

/// <summary>
/// Indicates if the message is a query or a response
/// </summary>
public enum QueryOrResponse : ushort
{
	Query = 0,
	Response = 1 << 15
}

public enum Opcode : ushort
{
	/// <summary>
	/// Standard query.
	/// </summary>
	Query = 0,
	/// <summary>
	/// aka. Reverse DNS Lookup
	/// </summary>
	IQuery = 1 << 11,
	/// <summary>
	/// Server status
	/// </summary>
	Status = 2 << 11,
	/// <summary>
	/// Specifies whether to delete, add or update RRs from a specified DNS zone. (RFC 2136)
	/// </summary>
	Update = 5 << 11
}

/// <summary>
/// Indicates if the name server is authoritative for the domain in the question section.
/// </summary>
public enum AuthoritativeAnswer : ushort
{
	No = 0,
	Yes = 1 << 10
}

/// <summary>
/// Indicates if the response was truncated.
/// </summary>
public enum Truncation : ushort
{
	No = 0,
	Yes = 1 << 9
}

/// <summary>
/// This flag tells the name server to handle the query itself, called a recursive query.
/// If the bit is not set, and the requested name server doesn't have an authoritative answer, the requested name server returns a list of other name servers to contact for the answer.
/// This is called an iterative query.
/// </summary>
public enum RecursionDesired : ushort
{
	No = 0,
	Yes = 1 << 8
}

/// <summary>
/// Indicates if the server supports recursion.
/// </summary>
public enum RecursionAvailable : ushort
{
	No = 0,
	Yes = 1 << 7
}

public enum ResponseCode : ushort
{
	NOERROR = 0,
	FORMERR = 1,
	SERVFAIL = 2,
	NXDOMAIN = 3,
	NOTIMP = 4,
	REFUSED = 5,
	YXDOMAIN = 6,
	YXRRSET = 7,
	NXRRSET = 8,
	NOTAUTH = 9,
	NOTZONE = 10
}