using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using TurnerSoftware.DinoDNS.Protocol;

namespace TurnerSoftware.DinoDNS.Benchmarks;

public static class TestServer
{
	private const int TIMEOUT_IN_SECONDS = 8;
	private const int DEFAULT_DNS_SERVER_PORT = 53;
	private readonly static IPEndPoint ENDPOINT = new(IPAddress.Any, DEFAULT_DNS_SERVER_PORT);

	public static void Start(string[] args)
	{
		_ = Task.Run(async () => await StartAsync(args));
		Task.Delay(500).Wait();
	}

	public static async ValueTask StartAsync(string[] args)
	{
		try
		{
			if (args.Length > 0 && args[0] == "tcp")
			{
				Console.WriteLine("TCP server started!");
				await RunTcpServerAsync();
			}
			else
			{
				Console.WriteLine("UDP server started!");
				await RunUdpServerAsync();
			}
		}
		catch (OperationCanceledException)
		{
			Console.WriteLine("Benchmark server has ended.");
		}
	}

	private static async ValueTask RunUdpServerAsync()
	{
		var exampleData = GenerateExampleData();
		var cancellationSource = new CancellationTokenSource();
		cancellationSource.CancelAfter(TimeSpan.FromSeconds(TIMEOUT_IN_SECONDS));

		var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		socket.Bind(ENDPOINT);
		var buffer = new byte[1024].AsMemory();
		while (true)
		{
			var result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, ENDPOINT, cancellationSource.Token);
			socket.SendTo(exampleData.Span, result.RemoteEndPoint);
			cancellationSource = new CancellationTokenSource();
			cancellationSource.CancelAfter(TimeSpan.FromSeconds(TIMEOUT_IN_SECONDS));
		}
	}

	private static async ValueTask RunTcpServerAsync()
	{
		var exampleData = GenerateExampleData();
		var tcpExampleData = new byte[exampleData.Length + 2].AsMemory();
		BinaryPrimitives.WriteUInt16BigEndian(tcpExampleData.Span, (ushort)exampleData.Length);
		exampleData.CopyTo(tcpExampleData[2..]);

		var cancellationSource = new CancellationTokenSource();
		cancellationSource.CancelAfter(TimeSpan.FromSeconds(TIMEOUT_IN_SECONDS));
		var listener = new TcpListener(ENDPOINT);
		try
		{
			listener.Start();

			var buffer = new byte[1024].AsMemory();
			Socket? socket = null;
			while (true)
			{
				if (socket is null || !socket.Connected)
				{
					socket?.Dispose();
					socket = await listener.AcceptSocketAsync(cancellationSource.Token);
				}

				try
				{
					var bytesReceived = await socket.ReceiveAsync(buffer[..2], SocketFlags.None, cancellationSource.Token);
					if (bytesReceived == 0)
					{
						socket.Shutdown(SocketShutdown.Both);
						socket.Dispose();
						continue;
					}
					var messageLength = BinaryPrimitives.ReadUInt16BigEndian(buffer.Span);
					await socket.ReceiveAsync(buffer[2..][..messageLength], SocketFlags.None, cancellationSource.Token);
					await socket.SendAsync(tcpExampleData, SocketFlags.None, cancellationSource.Token);
					cancellationSource = new CancellationTokenSource();
					cancellationSource.CancelAfter(TimeSpan.FromSeconds(TIMEOUT_IN_SECONDS));
				}
				catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
				{
					continue;
				}
			}
		}
		finally
		{
			listener.Stop();
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
}
