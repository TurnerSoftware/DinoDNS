using System.Net;
using System.Net.Sockets;

namespace TurnerSoftware.DinoDNS.Messengers;

public readonly record struct UdpMessenger(IPEndPoint Endpoint) : IDnsMessenger
{
	public async ValueTask<MessengerResult> SendMessageAsync(ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken)
	{
		using var socket = new Socket(Endpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
		await socket.ConnectAsync(Endpoint);
		await socket.SendAsync(sourceBuffer, SocketFlags.None, cancellationToken);
		var bytesReceived = await socket.ReceiveAsync(destinationBuffer, SocketFlags.None, cancellationToken);
		return new(bytesReceived, 0);
	}
}
