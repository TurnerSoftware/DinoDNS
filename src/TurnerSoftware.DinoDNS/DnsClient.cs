using System.Buffers;
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
		//TODO: Handle socket exceptions
		//		Handle typed-errors
		//		Handle truncation
		for (var i = 0; i < Messengers.Length; i++)
		{
			var messenger = Messengers[i];
			var result = await messenger.SendMessageAsync(sourceBuffer, destinationBuffer, cancellationToken);
			return result.BytesReceived;
		}
		throw new Exception("Uh oh");
	}

	public async ValueTask<DnsMessage> SendAsync(DnsMessage message, CancellationToken cancellationToken = default)
	{
		//We can't reuse the same buffer for sending and receiving as we may received an invalid payload back.
		//This would then clear data that we'd want to try again and send.
		var sourceBuffer = ArrayPool<byte>.Shared.Rent(Options.MaximumMessageSize);
		var destinationBuffer = ArrayPool<byte>.Shared.Rent(Options.MaximumMessageSize);

		var sourceMemory = sourceBuffer.AsMemory(); ;
		var destinationMemory = destinationBuffer.AsMemory();

		var writer = new DnsProtocolWriter(sourceMemory).AppendMessage(message);
		var bytesReceived = await SendAsync(sourceMemory, destinationMemory, cancellationToken);

		var reader = new DnsProtocolReader(destinationMemory[..bytesReceived]);
		reader.ReadMessage(out var receivedMessage);

		ArrayPool<byte>.Shared.Return(sourceBuffer);
		ArrayPool<byte>.Shared.Return(destinationBuffer);
		return receivedMessage;
	}
}
