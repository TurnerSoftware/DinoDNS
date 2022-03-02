using System.Net.Sockets;
using TurnerSoftware.DinoDNS.Connection;
using TurnerSoftware.DinoDNS.Protocol;

namespace TurnerSoftware.DinoDNS;

public sealed class DnsClient
{
	private readonly NameServer[] NameServers;
	public readonly DnsMessageOptions Options;

	public DnsClient(NameServer[] nameServers, DnsMessageOptions options)
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
		var transitData = TransitData.Rent(Options);
		var responseBuffer = transitData.ResponseBuffer;
		var writtenBytes = new DnsProtocolWriter(transitData.RequestBuffer).AppendMessage(message).GetWrittenBytes();

		try
		{
			var bytesReceived = await SendAsync(writtenBytes, responseBuffer, cancellationToken).ConfigureAwait(false);
			if (bytesReceived >= Header.Length)
			{
				//We must allocate and copy the data to avoid use-after-free issues with the rented bytes.
				var returnSafeBuffer = new byte[bytesReceived].AsMemory();
				responseBuffer[..bytesReceived].CopyTo(returnSafeBuffer);

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
			TransitData.Return(transitData);
		}
	}

	public async ValueTask<int> SendAsync(ReadOnlyMemory<byte> requestBuffer, Memory<byte> responseBuffer, CancellationToken cancellationToken = default)
	{
		foreach (var nameServer in NameServers)
		{
			try
			{
				var connection = nameServer.Connection;
				var bytesReceived = await connection
					.SendMessageAsync(nameServer.EndPoint, requestBuffer, responseBuffer, cancellationToken)
					.ConfigureAwait(false);
				
				new DnsProtocolReader(responseBuffer).ReadHeader(out var header);
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
