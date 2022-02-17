using System.Buffers;
using System.Net.Sockets;
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

	public async ValueTask<DnsMessage> SendAsync(DnsMessage message, CancellationToken cancellationToken = default)
	{
		//We can't reuse the same buffer for sending and receiving as we may received an invalid payload back.
		//This would then clear data that we'd want to try again and send.
		//We can share the single instance of the buffer if we split it in half - this performs maginally faster than renting two arrays.
		var readWriteBuffer = ArrayPool<byte>.Shared.Rent(Options.MaximumMessageSize * 2);
		var sourceBuffer = readWriteBuffer.AsMemory(0, Options.MaximumMessageSize);
		var destinationBuffer = readWriteBuffer.AsMemory(Options.MaximumMessageSize);

		var writtenBytes = new DnsProtocolWriter(sourceBuffer).AppendMessage(message).GetWrittenBytes();

		try
		{
			var bytesReceived = await SendAsync(writtenBytes, destinationBuffer, cancellationToken).ConfigureAwait(false);
			if (bytesReceived >= Header.Length)
			{
				//We must allocate and copy the data to avoid use-after-free issues with the rented ArrayPool bytes.
				var returnSafeBuffer = new byte[bytesReceived].AsMemory();
				destinationBuffer[..bytesReceived].CopyTo(returnSafeBuffer);

				var reader = new DnsProtocolReader(returnSafeBuffer);
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
			ArrayPool<byte>.Shared.Return(readWriteBuffer);
		}
	}

	public async ValueTask<int> SendAsync(ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken)
	{
		foreach (var nameServer in NameServers)
		{
			try
			{
				var connection = nameServer.Connection;
				var bytesReceived = await connection
					.SendMessageAsync(nameServer.EndPoint, sourceBuffer, destinationBuffer, cancellationToken)
					.ConfigureAwait(false);

				new DnsProtocolReader(destinationBuffer).ReadHeader(out var header);
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
