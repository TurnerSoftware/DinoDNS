using System.Net;
using TurnerSoftware.DinoDNS.Protocol;

namespace TurnerSoftware.DinoDNS.Connection;

public sealed class UdpTcpConnectionClient : IDnsConnectionClient
{
	public static readonly UdpTcpConnectionClient Instance = new();

	public async ValueTask<int> SendMessageAsync(IPEndPoint endPoint, ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken)
	{
		var messageLength = await UdpConnectionClient.Instance.SendMessageAsync(endPoint, sourceBuffer, destinationBuffer, cancellationToken).ConfigureAwait(false);

		new DnsProtocolReader(destinationBuffer).ReadHeader(out var header);
		if (header.Flags.Truncation == Truncation.Yes)
		{
			messageLength = await TcpConnectionClient.Instance.SendMessageAsync(endPoint, sourceBuffer, destinationBuffer, cancellationToken).ConfigureAwait(false); 
		}

		return messageLength;
	}
}
