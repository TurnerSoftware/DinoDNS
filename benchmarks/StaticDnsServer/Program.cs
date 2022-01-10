using System.Net;
using System.Net.Sockets;
using TurnerSoftware.DinoDNS;
using TurnerSoftware.DinoDNS.Protocol;

const int TIMEOUT_IN_SECONDS = 8;

Console.WriteLine("Full Stack DNS Benchmark Server");
Console.WriteLine("===============================");
Console.WriteLine("This is designed to respond as fast as possible with a static payload for a DNS query.");
Console.WriteLine($"This server will end within {TIMEOUT_IN_SECONDS} seconds of the last request.");

try
{
	await RunUdpServerAsync();
}
catch (OperationCanceledException)
{
	Console.WriteLine("Benchmark server has ended.");
}

static async ValueTask RunUdpServerAsync()
{
	Console.WriteLine("UDP server started!");
	var exampleData = GenerateExampleData();
	var cancellationSource = new CancellationTokenSource();
	cancellationSource.CancelAfter(TimeSpan.FromSeconds(TIMEOUT_IN_SECONDS));

	var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
	socket.Bind(Statics.ENDPOINT);
	var buffer = new byte[1024].AsMemory();
	while (true)
	{
		var result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, Statics.ENDPOINT, cancellationSource.Token);
		socket.SendTo(exampleData.Span, result.RemoteEndPoint);
		cancellationSource = new CancellationTokenSource();
		cancellationSource.CancelAfter(TimeSpan.FromSeconds(TIMEOUT_IN_SECONDS));
	}
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

public static class Statics
{
	public const int DEFAULT_DNS_SERVER_PORT = 53;
	public readonly static IPEndPoint ENDPOINT = new(IPAddress.Any, DEFAULT_DNS_SERVER_PORT);
}