using System.Net;
using System.Net.Sockets;
using TurnerSoftware.DinoDNS;
using TurnerSoftware.DinoDNS.Protocol;

const int DEFAULT_DNS_SERVER_PORT = 53;

Console.WriteLine("Full Stack DNS Benchmark Server");
Console.WriteLine("This is designed to respond as fast as possible with a static payload for a DNS query.");

using var udpClient = new UdpClient(DEFAULT_DNS_SERVER_PORT);
var endpoint = new IPEndPoint(0, 0);

var exampleData = GenerateExampleData();

Console.WriteLine("Now listening for DNS requests...");

while (true)
{
	_ = udpClient.Receive(ref endpoint);
	udpClient.Send(exampleData.Span, endpoint);
}

static ReadOnlyMemory<byte> GenerateExampleData()
{
	var requestMessage = DnsMessage.CreateQuery(44124)
		.WithQuestions(new Question[]
		{
			new()
			{
				Query = new LabelSequence("test.www.example.org"),
				Type = DnsQueryType.A,
				Class = DnsClass.IN
			}
		});

	var responseMessage = DnsMessage.CreateResponse(requestMessage, ResponseCode.NOERROR)
		.WithAnswers(new ResourceRecord[]
		{
			new()
			{
				DomainName = new LabelSequence("test.www.example.org"),
				Type = DnsType.A,
				Class = DnsClass.IN,
				TimeToLive = 1800,
				ResourceDataLength = 4,
				Data = new byte[] { 192, 168, 0, 1 }
			}
		});

	var buffer = new byte[1024];
	var messageBytes = new DnsProtocolWriter(buffer.AsMemory())
		.AppendMessage(responseMessage)
		.GetWrittenBytes()
		.ToArray();
	return messageBytes;
}