using System.Net;
using TurnerSoftware.DinoDNS.Protocol;

namespace TurnerSoftware.DinoDNS.Connection;

public sealed class UdpTcpConnectionClient : IDnsConnectionClient
{
	public static readonly UdpTcpConnectionClient Instance = new();

	public async ValueTask<int> SendMessageAsync(IPEndPoint endPoint, ReadOnlyMemory<byte> requestBuffer, Memory<byte> responseBuffer, CancellationToken cancellationToken)
	{
		var messageLength = await UdpConnectionClient.Instance.SendMessageAsync(endPoint, requestBuffer, responseBuffer, cancellationToken).ConfigureAwait(false);

		new DnsProtocolReader(responseBuffer).ReadHeader(out var header);
		if (header.Flags.Truncation == Truncation.Yes)
		{
			messageLength = await TcpConnectionClient.Instance.SendMessageAsync(endPoint, requestBuffer, responseBuffer, cancellationToken).ConfigureAwait(false); 
		}

		return messageLength;
	}
}
