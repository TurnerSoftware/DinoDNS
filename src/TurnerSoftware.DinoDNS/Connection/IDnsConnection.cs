using System.Net;

namespace TurnerSoftware.DinoDNS.Connection;

public interface IDnsConnection
{
	ValueTask<int> SendMessageAsync(IPEndPoint endPoint, ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken);
}
