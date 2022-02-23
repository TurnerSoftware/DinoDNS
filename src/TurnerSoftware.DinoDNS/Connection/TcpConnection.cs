using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace TurnerSoftware.DinoDNS.Connection;

public class TcpConnectionClient : IDnsConnectionClient
{
	public static readonly TcpConnectionClient Instance = new();

	private readonly ConcurrentDictionary<IPEndPoint, ConcurrentQueue<Socket>> Sockets = new();

	protected virtual Socket CreateSocket(IPEndPoint endPoint) => new(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
	{
		SendBufferSize = 2048,
		ReceiveBufferSize = 2048,
		SendTimeout = 10,
		ReceiveTimeout = 10
	};

	protected virtual ValueTask OnConnectAsync(Socket socket, IPEndPoint endPoint, CancellationToken cancellationToken) => ValueTask.CompletedTask;

	protected virtual void OnSocketEnd(Socket socket) { }

	public async ValueTask<int> SendMessageAsync(IPEndPoint endPoint, ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken)
	{
		var socketQueue = Sockets.GetOrAdd(endPoint, static _ => new());

		if (socketQueue.TryDequeue(out var socket))
		{
			if (!socket.Connected)
			{
				//TODO: Investigate whether we can just re-connect to existing sockets that are closed
				OnSocketEnd(socket);
				socket.Dispose();
				socket = CreateSocket(endPoint);
			}
		}
		else if (socket is null)
		{
			socket = CreateSocket(endPoint);
		}

		if (!socket.Connected)
		{
			await socket.ConnectAsync(endPoint).ConfigureAwait(false);
			await OnConnectAsync(socket, endPoint, cancellationToken).ConfigureAwait(false);
		}

		try
		{
			return await PerformQueryAsync(socket, sourceBuffer, destinationBuffer, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			socketQueue.Enqueue(socket);
		}
	}

	protected virtual async ValueTask<int> PerformQueryAsync(Socket socket, ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken)
	{
		//TCP connections require sending a 2-byte length value before the message.
		//Use our destination buffer as a temporary buffer to get and send the length.
		BinaryPrimitives.WriteUInt16BigEndian(destinationBuffer.Span, (ushort)sourceBuffer.Length);
		await socket.SendAsync(destinationBuffer[..2], SocketFlags.None, cancellationToken).ConfigureAwait(false);
		//Send our main message from our source buffer	
		await socket.SendAsync(sourceBuffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);

		//Read the corresponding 2-byte length in the response to know how long the message is
		await socket.ReceiveAsync(destinationBuffer[..2], SocketFlags.None, cancellationToken).ConfigureAwait(false);
		var messageLength = BinaryPrimitives.ReadUInt16BigEndian(destinationBuffer.Span);
		//Read the response based on the determined message length
		await socket.ReceiveAsync(destinationBuffer[..messageLength], SocketFlags.None, cancellationToken).ConfigureAwait(false);
		return messageLength;
	}
}


public class TcpConnectionServer : IDnsConnectionServer
{
	public async Task ListenAsync(IPEndPoint endPoint, OnDnsQueryCallback callback, DnsMessageOptions options, CancellationToken cancellationToken)
	{
		var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
		socket.Listen();
		
		while (!cancellationToken.IsCancellationRequested)
		{
			var newSocket = await socket.AcceptAsync(cancellationToken).ConfigureAwait(false);
			_ = HandleSocketAsync(newSocket, callback, options, cancellationToken).ConfigureAwait(false);
		}
	}

	private static async Task HandleSocketAsync(Socket socket, OnDnsQueryCallback callback, DnsMessageOptions options, CancellationToken cancellationToken)
	{
		//TODO: Investigate whether multiple requests can/should be handled at a time on a single socket.
		//		Technically it should be possible in the same way the UDP server does it.

		var rentedBytes = ArrayPool<byte>.Shared.Rent(options.MaximumMessageSize * 2);
		try
		{
			var requestBuffer = rentedBytes.AsMemory(0, options.MaximumMessageSize);
			var responseBuffer = rentedBytes.AsMemory(options.MaximumMessageSize);
			while (true)
			{
				//Read the corresponding 2-byte length in the request to know how long the message is
				await socket.ReceiveAsync(requestBuffer[..2], SocketFlags.None, cancellationToken).ConfigureAwait(false);
				var messageLength = BinaryPrimitives.ReadUInt16BigEndian(requestBuffer.Span);
				//Read the request based on the determined message length
				await socket.ReceiveAsync(requestBuffer[..messageLength], SocketFlags.None, cancellationToken).ConfigureAwait(false);

				var bytesWritten = await callback(requestBuffer, responseBuffer, cancellationToken).ConfigureAwait(false);

				//TCP connections require sending a 2-byte length value before the message.
				//Use our request buffer as a temporary buffer to get and send the length.
				BinaryPrimitives.WriteUInt16BigEndian(requestBuffer.Span, (ushort)bytesWritten);
				await socket.SendAsync(requestBuffer[..2], SocketFlags.None, cancellationToken).ConfigureAwait(false);
				//Send our main message from our response buffer	
				await socket.SendAsync(responseBuffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(rentedBytes);
		}
	}
}
