using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace TurnerSoftware.DinoDNS.Connection;

public sealed class UdpConnectionClient : IDnsConnectionClient
{
	public static readonly UdpConnectionClient Instance = new();

	private readonly ConcurrentDictionary<IPEndPoint, ConcurrentQueue<Socket>> Sockets = new();

	public async ValueTask<int> SendMessageAsync(IPEndPoint endPoint, ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken)
	{
		var socketQueue = Sockets.GetOrAdd(endPoint, static _ => new());
		if (!socketQueue.TryDequeue(out var socket))
		{
			socket = new Socket(endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
			//There is no IO involved in connecting to a connection-less protocol
			socket.Connect(endPoint);
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

	public async Task ListenAsync(IPEndPoint endPoint, OnDnsQueryCallback callback, DnsMessageOptions options, CancellationToken cancellationToken)
	{
		var socket = new Socket(endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
		socket.Bind(endPoint);

		while (!cancellationToken.IsCancellationRequested)
		{
			var rentedBytes = ArrayPool<byte>.Shared.Rent(options.MaximumMessageSize * 2);
			var requestBuffer = rentedBytes.AsMemory(0, options.MaximumMessageSize);
			var socketReceived = await socket.ReceiveFromAsync(requestBuffer, SocketFlags.None, endPoint, cancellationToken).ConfigureAwait(false);
			_ = HandleRequestAsync(socket, rentedBytes, socketReceived, callback, options, cancellationToken).ConfigureAwait(false);
		}

		static async Task HandleRequestAsync(Socket socket, byte[] rentedBytes, SocketReceiveFromResult socketReceived, OnDnsQueryCallback callback, DnsMessageOptions options, CancellationToken cancellationToken)
		{
			try
			{
				var requestBuffer = rentedBytes.AsMemory(0, options.MaximumMessageSize);
				var responseBuffer = rentedBytes.AsMemory(options.MaximumMessageSize);
				var bytesWritten = await callback(requestBuffer, responseBuffer, cancellationToken).ConfigureAwait(false);
				await socket.SendToAsync(responseBuffer, SocketFlags.None, socketReceived.RemoteEndPoint, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(rentedBytes);
			}
		}
	}
}

public sealed class UdpConnectionServer : IDnsConnectionServer
{
	public static readonly UdpConnectionServer Instance = new();

	public async Task ListenAsync(IPEndPoint endPoint, OnDnsQueryCallback callback, DnsMessageOptions options, CancellationToken cancellationToken)
	{
		var socket = new Socket(endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
		socket.Bind(endPoint);

		while (!cancellationToken.IsCancellationRequested)
		{
			var rentedBytes = ArrayPool<byte>.Shared.Rent(options.MaximumMessageSize * 2);
			var requestBuffer = rentedBytes.AsMemory(0, options.MaximumMessageSize);
			var socketReceived = await socket.ReceiveFromAsync(requestBuffer, SocketFlags.None, endPoint, cancellationToken).ConfigureAwait(false);
			_ = HandleRequestAsync(socket, rentedBytes, socketReceived, callback, options, cancellationToken).ConfigureAwait(false);
		}

		static async Task HandleRequestAsync(Socket socket, byte[] rentedBytes, SocketReceiveFromResult socketReceived, OnDnsQueryCallback callback, DnsMessageOptions options, CancellationToken cancellationToken)
		{
			try
			{
				var requestBuffer = rentedBytes.AsMemory(0, options.MaximumMessageSize);
				var responseBuffer = rentedBytes.AsMemory(options.MaximumMessageSize);
				var bytesWritten = await callback(requestBuffer, responseBuffer, cancellationToken).ConfigureAwait(false);
				await socket.SendToAsync(responseBuffer, SocketFlags.None, socketReceived.RemoteEndPoint, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(rentedBytes);
			}
		}
	}
}