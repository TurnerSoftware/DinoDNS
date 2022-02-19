using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace TurnerSoftware.DinoDNS.Connection;

public sealed class UdpConnection : IDnsConnection
{
	public static readonly UdpConnection Instance = new();

	private readonly ConcurrentDictionary<IPEndPoint, ConcurrentQueue<Socket>> Sockets = new();

	public async ValueTask<int> SendMessageAsync(IPEndPoint endPoint, ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken)
	{
		var socketQueue = Sockets.GetOrAdd(endPoint, static _ => new());
		if (!socketQueue.TryDequeue(out var socket))
		{
			socket = new Socket(endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
			await socket.ConnectAsync(endPoint).ConfigureAwait(false);
		}

		try
		{
			await socket.SendAsync(sourceBuffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
			var messageLength = await socket.ReceiveAsync(destinationBuffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
			return messageLength;
		}
		finally
		{
			socketQueue.Enqueue(socket);
		}
	}
}
