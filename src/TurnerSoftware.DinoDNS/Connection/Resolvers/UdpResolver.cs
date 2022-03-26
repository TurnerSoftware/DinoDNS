using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace TurnerSoftware.DinoDNS.Connection;

public sealed class UdpResolver : IDnsResolver
{
	public static readonly UdpResolver Instance = new();

	private readonly ConcurrentDictionary<IPEndPoint, Socket> Sockets = new();
	private readonly object NewSocketLock = new();

	private Socket GetSocket(IPEndPoint endPoint)
	{
		if (Sockets.TryGetValue(endPoint, out var socket))
		{
			return socket;
		}

		//We can't rely on GetOrAdd-type methods on ConcurrentDictionary as the factory can be called multiple times.
		//Instead, we rely on TryGetValue for the hot path (existing socket) otherwise use a typical lock.
		lock (NewSocketLock)
		{
			if (!Sockets.TryGetValue(endPoint, out socket))
			{
				socket = new Socket(endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
				//There is no IO involved in connecting to a connection-less protocol
				socket.Connect(endPoint);

				Sockets.TryAdd(endPoint, socket);
			}

			return socket;
		}
	}

	public async ValueTask<int> SendMessageAsync(IPEndPoint endPoint, ReadOnlyMemory<byte> requestBuffer, Memory<byte> responseBuffer, CancellationToken cancellationToken)
	{
		var socket = GetSocket(endPoint);

		await socket.SendAsync(requestBuffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
		var messageLength = await socket.ReceiveAsync(responseBuffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);

		if (SocketMessageOrderer.CheckMessageId(requestBuffer, responseBuffer) == MessageIdResult.Mixed)
		{
			messageLength = SocketMessageOrderer.Exchange(
				socket,
				requestBuffer,
				responseBuffer,
				messageLength,
				cancellationToken
			);
		}

		return messageLength;
	}
}
