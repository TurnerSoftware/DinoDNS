using System.Buffers;
using System.Net.Sockets;
using TurnerSoftware.DinoDNS.Messengers;
using TurnerSoftware.DinoDNS.Protocol;

namespace TurnerSoftware.DinoDNS;

public class DnsClient
{
	private readonly IDnsMessenger[] Messengers;
	public readonly DnsClientOptions Options;

	public DnsClient(IDnsMessenger[] messengers, DnsClientOptions options)
	{
		if (messengers is null || messengers.Length == 0)
		{
			throw new ArgumentException("Invalid number of DNS messengers configured.");
		}

		if (!options.Validate(out var errorMessage))
		{
			throw new ArgumentException($"Invalid DNS client options. {errorMessage}");
		}

		Messengers = messengers;
		Options = options;
	}

	public async ValueTask<int> SendAsync(ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken)
	{
		for (var i = 0; i < Messengers.Length; i++)
		{
			try
			{
				var messenger = Messengers[i];
				var bytesReceived = await messenger.SendMessageAsync(sourceBuffer, destinationBuffer, cancellationToken).ConfigureAwait(false);

				//Check truncation, falling back to the next configured messenger
				new DnsProtocolReader(destinationBuffer).ReadHeader(out var header);
				if (header.Flags.Truncation == Truncation.Yes)
				{
					continue;
				}

				switch (header.Flags.ResponseCode)
				{
					case ResponseCode.SERVFAIL:
					case ResponseCode.NOTIMP:
					case ResponseCode.REFUSED:
						continue;
					case ResponseCode.FORMERR:
						throw new FormatException("Name server responded with a Format Error");
				}

				return bytesReceived;
			}
			catch (SocketException ex) when (ex.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionAborted)
			{
				//Allow certain types of socket errors to silently continue to the next messenger.
				continue;
			}
		}

		throw new Exception("Unable to communicate with ");
	}

	public async ValueTask<DnsMessage> SendAsync(DnsMessage message, CancellationToken cancellationToken = default)
	{
		//We can't reuse the same buffer for sending and receiving as we may received an invalid payload back.
		//This would then clear data that we'd want to try again and send.
		var sourceBuffer = ArrayPool<byte>.Shared.Rent(Options.MaximumMessageSize);
		var destinationBuffer = ArrayPool<byte>.Shared.Rent(Options.MaximumMessageSize);

		var sourceMemory = sourceBuffer.AsMemory(); ;
		var destinationMemory = destinationBuffer.AsMemory();

		var writtenBytes = new DnsProtocolWriter(sourceMemory).AppendMessage(message).GetWrittenBytes();
		var bytesReceived = await SendAsync(writtenBytes, destinationMemory, cancellationToken).ConfigureAwait(false);

		var reader = new DnsProtocolReader(destinationMemory[..bytesReceived]);
		reader.ReadMessage(out var receivedMessage);

		ArrayPool<byte>.Shared.Return(sourceBuffer);
		ArrayPool<byte>.Shared.Return(destinationBuffer);
		return receivedMessage;
	}
}
