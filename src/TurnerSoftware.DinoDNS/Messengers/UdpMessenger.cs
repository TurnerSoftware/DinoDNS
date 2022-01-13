using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace TurnerSoftware.DinoDNS.Messengers;

public readonly record struct UdpMessenger(IPEndPoint Endpoint) : IDnsMessenger
{
	private static readonly ConcurrentQueue<Socket> Sockets4 = new();
	private static readonly ConcurrentQueue<Socket> Sockets6 = new();

	public async ValueTask<int> SendMessageAsync(ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken)
	{
		var socketQueue = Endpoint.AddressFamily == AddressFamily.InterNetwork ? Sockets4 : Sockets6;
		if (!socketQueue.TryDequeue(out var socket))
		{
			socket = new Socket(Endpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
		}

		try
		{
			await socket.SendToAsync(sourceBuffer, SocketFlags.None, Endpoint, cancellationToken).ConfigureAwait(false);
			var result = await socket.ReceiveFromAsync(destinationBuffer, SocketFlags.None, Endpoint, cancellationToken).ConfigureAwait(false);
			return result.ReceivedBytes;
		}
		finally
		{
			socketQueue.Enqueue(socket);
		}
	}
}
