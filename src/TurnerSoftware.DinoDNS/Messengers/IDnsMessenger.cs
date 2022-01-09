namespace TurnerSoftware.DinoDNS.Messengers;

public interface IDnsMessenger
{
	ValueTask<MessengerResult> SendMessageAsync(ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken);
}
