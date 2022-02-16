using System.Net;
using TurnerSoftware.DinoDNS.Protocol;

namespace TurnerSoftware.DinoDNS.Connection;

public sealed class UdpTcpConnection : IDnsConnection
{
	public static readonly UdpTcpConnection Instance = new();

	public async ValueTask<int> SendMessageAsync(IPEndPoint endPoint, ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken)
	{
		var messageLength = await UdpConnection.Instance.SendMessageAsync(endPoint, sourceBuffer, destinationBuffer, cancellationToken).ConfigureAwait(false);

		new DnsProtocolReader(destinationBuffer).ReadHeader(out var header);
		if (header.Flags.Truncation == Truncation.Yes)
		{
			messageLength = await TcpConnection.Instance.SendMessageAsync(endPoint, sourceBuffer, destinationBuffer, cancellationToken).ConfigureAwait(false); 
		}

		return messageLength;
	}
}
