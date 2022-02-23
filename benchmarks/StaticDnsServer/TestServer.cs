using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using TurnerSoftware.DinoDNS.Protocol;

namespace TurnerSoftware.DinoDNS.Benchmarks;

public static class TestServer
{
	private const int DEFAULT_DNS_SERVER_PORT = 53;
	private readonly static IPEndPoint ENDPOINT = new(IPAddress.Any, DEFAULT_DNS_SERVER_PORT);

	private static readonly ResourceRecord[] Answers = new ResourceRecord[]
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
	};

	public static async ValueTask StartAsync(string[] args)
	{
		try
		{
			var cancellationTokenSource = new CancellationTokenSource();
			_ = Task.Run(() => {
				Console.Read();
				cancellationTokenSource.Cancel();
			});

			Connection.IDnsConnectionServer? server = null;
			var protocol = args.Length > 0 ? args[0] : "udp";
			switch (protocol)
			{
				case "tcp":
					Console.WriteLine("TCP server started!");
					server = Connection.TcpConnectionServer.Instance;
					break;
				case "udp":
				default:
					Console.WriteLine("UDP server started!");
					server = Connection.UdpConnectionServer.Instance;
					break;
			}

			await server.ListenAsync(ENDPOINT, (requestBuffer, responseBuffer, token) =>
			{
				new DnsProtocolReader(requestBuffer).ReadMessage(out var message);
				var responseMessage = DnsMessage.CreateResponse(message, ResponseCode.NOERROR).WithAnswers(Answers);
				var writer = new DnsProtocolWriter(responseBuffer).AppendMessage(responseMessage);
				return ValueTask.FromResult(writer.BytesWritten);
			}, DnsMessageOptions.Default, cancellationTokenSource.Token);
		}
		catch (OperationCanceledException)
		{
			Console.WriteLine("Benchmark server has ended.");
		}
	}
}
