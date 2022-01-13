namespace TurnerSoftware.DinoDNS.Messengers;

public interface IDnsMessenger
{
	ValueTask<int> SendMessageAsync(ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken);
}
