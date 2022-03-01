using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace TurnerSoftware.DinoDNS.Connection;

public sealed class UdpConnectionClient : IDnsConnectionClient
{
	public static readonly UdpConnectionClient Instance = new();

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

	public async ValueTask<int> SendMessageAsync(IPEndPoint endPoint, ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken)
	{
		var socket = GetSocket(endPoint);

		await socket.SendAsync(sourceBuffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
		var messageLength = await socket.ReceiveAsync(destinationBuffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);

		if (SocketMessageOrderer.CheckMessageId(sourceBuffer, destinationBuffer) == MessageIdResult.Mixed)
		{
			messageLength = SocketMessageOrderer.Exchange(
				socket,
				sourceBuffer,
				destinationBuffer,
				messageLength,
				cancellationToken
			);
		}

		return messageLength;
	}
}

public sealed class UdpConnectionServer : IDnsConnectionServer
{
	public static readonly UdpConnectionServer Instance = new();

	public async Task ListenAsync(IPEndPoint endPoint, OnDnsQueryCallback callback, DnsMessageOptions options, CancellationToken cancellationToken)
	{
		using var socket = new Socket(endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
		socket.Bind(endPoint);

		while (!cancellationToken.IsCancellationRequested)
		{
			var transitData = TransitData.Rent(options);
			var socketReceived = await socket.ReceiveFromAsync(transitData.RequestBuffer, SocketFlags.None, endPoint, cancellationToken).ConfigureAwait(false);
			_ = HandleRequestAsync(socket, transitData, socketReceived, callback, options, cancellationToken).ConfigureAwait(false);
		}

		static async Task HandleRequestAsync(Socket socket, TransitData transitData, SocketReceiveFromResult socketReceived, OnDnsQueryCallback callback, DnsMessageOptions options, CancellationToken cancellationToken)
		{
			try
			{
				var (requestBuffer, responseBuffer) = transitData;
				var bytesWritten = await callback(requestBuffer, responseBuffer, cancellationToken).ConfigureAwait(false);
				await socket.SendToAsync(transitData.ResponseBuffer[..bytesWritten], SocketFlags.None, socketReceived.RemoteEndPoint, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				TransitData.Return(transitData);
			}
		}
	}
}