using System.Buffers;
using System.Net.Sockets;
using TurnerSoftware.DinoDNS.Connection;
using TurnerSoftware.DinoDNS.Protocol;

namespace TurnerSoftware.DinoDNS;

public sealed class DnsClient
{
	private readonly NameServer[] NameServers;
	public readonly DnsClientOptions Options;

	public DnsClient(NameServer[] nameServers, DnsClientOptions options)
	{
		if (nameServers is null || nameServers.Length == 0)
		{
			throw new ArgumentException("Invalid number of name servers configured.");
		}

		if (!options.Validate(out var errorMessage))
		{
			throw new ArgumentException($"Invalid DNS client options. {errorMessage}");
		}

		NameServers = nameServers;
		Options = options;
	}

	private static IDnsConnection GetConnection(ConnectionType connectionType) => connectionType switch
	{
		ConnectionType.Udp => UdpConnection.Instance,
		ConnectionType.Tcp => TcpConnection.Instance,
		_ => throw new NotImplementedException()
	};

	public async ValueTask<DnsMessage> SendAsync(DnsMessage message, CancellationToken cancellationToken = default)
	{
		//We can't reuse the same buffer for sending and receiving as we may received an invalid payload back.
		//This would then clear data that we'd want to try again and send.
		var sourceBuffer = ArrayPool<byte>.Shared.Rent(Options.MaximumMessageSize);
		var destinationBuffer = ArrayPool<byte>.Shared.Rent(Options.MaximumMessageSize);

		var sourceMemory = sourceBuffer.AsMemory();
		var destinationMemory = destinationBuffer.AsMemory();

		var writtenBytes = new DnsProtocolWriter(sourceMemory).AppendMessage(message).GetWrittenBytes();

		try
		{
			var bytesReceived = await SendAsync(writtenBytes, destinationMemory, cancellationToken).ConfigureAwait(false);
			if (bytesReceived >= Header.Length)
			{
				var reader = new DnsProtocolReader(destinationMemory[..bytesReceived]);
				reader.ReadMessage(out var receivedMessage);
				return receivedMessage;
			}
			else
			{
				throw new IOException("No bytes received");
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(sourceBuffer);
			ArrayPool<byte>.Shared.Return(destinationBuffer);
		}
	}

	public async ValueTask<int> SendAsync(ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken)
	{
		foreach (var nameServer in NameServers)
		{
			try
			{
				var connection = GetConnection(nameServer.ConnectionType);
				var bytesReceived = await connection
					.SendMessageAsync(nameServer.EndPoint, sourceBuffer, destinationBuffer, cancellationToken)
					.ConfigureAwait(false);

				//Check truncation, falling back to the next configured messenger
				new DnsProtocolReader(destinationBuffer).ReadHeader(out var header);
				//if (header.Flags.Truncation == Truncation.Yes)
				//{
				//	continue;
				//}

				switch (header.Flags.ResponseCode)
				{
					case ResponseCode.SERVFAIL:
					case ResponseCode.NOTIMP:
					case ResponseCode.REFUSED:
						//Try the next name server
						continue;
					case ResponseCode.FORMERR:
						//If we get a format error with one server, we will likely get it for all
						throw new FormatException("There was a format error with your query.");
				}

				return bytesReceived;
			}
			catch (SocketException ex) when (ex.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionAborted)
			{
				//Allow certain types of socket errors to silently continue to the next name server.
				continue;
			}
		}

		throw new Exception("No name servers are reachable");
	}
}
