namespace TurnerSoftware.DinoDNS.Protocol;

public readonly record struct DnsMessage(
	Header Header, 
	Question[] Questions,
	ResourceRecord[] Answers,
	ResourceRecord[] Authorities,
	ResourceRecord[] AdditionalRecords
)
{
	public static DnsMessage CreateQuery(ushort identification, Opcode opcode, RecursionDesired recursionDesired) => new()
	{
		Header = new()
		{
			Identification = identification,
			Flags = new()
			{
				QueryOrResponse = QueryOrResponse.Query,
				Opcode = opcode,
				RecursionDesired = recursionDesired
			}
		}
	};
}
