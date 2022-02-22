using System.Net;

namespace TurnerSoftware.DinoDNS.Connection;

public interface IDnsConnectionClient
{
	ValueTask<int> SendMessageAsync(IPEndPoint endPoint, ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken);
}

public delegate ValueTask<int> OnDnsQueryCallback(ReadOnlyMemory<byte> requestBuffer, Memory<byte> responseBuffer, CancellationToken cancellationToken);

public interface IDnsConnectionServer
{
	Task ListenAsync(IPEndPoint endPoint, OnDnsQueryCallback callback, DnsMessageOptions options, CancellationToken cancellationToken);
}
