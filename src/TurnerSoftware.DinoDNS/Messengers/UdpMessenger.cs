using System.Net;
using System.Net.Sockets;

namespace TurnerSoftware.DinoDNS.Messengers;

public readonly record struct UdpMessenger(IPEndPoint Endpoint) : IDnsMessenger
{
	public async ValueTask<MessengerResult> SendMessageAsync(ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken)
	{
		using var client = new UdpClient();
		await client.SendAsync(sourceBuffer, Endpoint, cancellationToken);
		var bytesReceived = await client.Client.ReceiveAsync(destinationBuffer, SocketFlags.None, cancellationToken);
		return new(bytesReceived, 0);
	}
}
