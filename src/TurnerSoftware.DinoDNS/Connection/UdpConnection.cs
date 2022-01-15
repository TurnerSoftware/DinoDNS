using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace TurnerSoftware.DinoDNS.Connection;

public sealed class UdpConnection : IDnsConnection
{
	private static readonly ConcurrentQueue<Socket> Sockets4 = new();
	private static readonly ConcurrentQueue<Socket> Sockets6 = new();

	public static readonly UdpConnection Instance = new();

	public async ValueTask<int> SendMessageAsync(IPEndPoint endPoint, ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken)
	{
		var socketQueue = endPoint.AddressFamily == AddressFamily.InterNetwork ? Sockets4 : Sockets6;
		if (!socketQueue.TryDequeue(out var socket))
		{
			socket = new Socket(endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
		}

		try
		{
			await socket.SendToAsync(sourceBuffer, SocketFlags.None, endPoint, cancellationToken).ConfigureAwait(false);
			var result = await socket.ReceiveFromAsync(destinationBuffer, SocketFlags.None, endPoint, cancellationToken).ConfigureAwait(false);
			return result.ReceivedBytes;
		}
		finally
		{
			socketQueue.Enqueue(socket);
		}
	}
}
