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
			await socket.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
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
	public static readonly TcpConnectionServer Instance = new();

	public async Task ListenAsync(IPEndPoint endPoint, OnDnsQueryCallback callback, DnsMessageOptions options, CancellationToken cancellationToken)
	{
		using var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
		socket.Bind(endPoint);
		socket.Listen();

		while (!cancellationToken.IsCancellationRequested)
		{
			var newSocket = await socket.AcceptAsync(cancellationToken).ConfigureAwait(false);
			_ = HandleSocketAsync(newSocket, callback, options, cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task HandleSocketAsync(Socket socket, OnDnsQueryCallback callback, DnsMessageOptions options, CancellationToken cancellationToken)
	{
		try
		{
			await OnConnectAsync(socket, cancellationToken).ConfigureAwait(false);
			using var writerLock = new SemaphoreSlim(1);
			while (true)
			{
				var transitData = TransitData.Rent(options);
				var hasReadData = false;
				try
				{
					var bytesRead = await ReadRequestAsync(socket, transitData.RequestBuffer, cancellationToken).ConfigureAwait(false);
					if (bytesRead == 0)
					{
						socket.Shutdown(SocketShutdown.Both);
						socket.Dispose();
						return;
					}

					hasReadData = true;
					_ = HandleRequestAsync(socket, callback, transitData, writerLock, cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					if (!hasReadData)
					{
						//Returning transit data only when data hasn't been read.
						//Once data has been read, the responsibility for returning the data belongs in the request handling.
						TransitData.Return(transitData);
					}
				}
			}
		}
		catch (Exception ex)
		{
			//TODO: Logger
			Console.WriteLine($"Socket:{ex.Message}");
		}
		finally
		{
			OnSocketEnd(socket);
		}
	}

	private async Task HandleRequestAsync(Socket socket, OnDnsQueryCallback callback, TransitData transitData, SemaphoreSlim writerLock, CancellationToken cancellationToken)
	{
		try
		{
			var (requestBuffer, responseBuffer) = transitData;
			var bytesWritten = await callback(requestBuffer, responseBuffer, cancellationToken).ConfigureAwait(false);
			await writerLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			await WriteResponseAsync(socket, responseBuffer[..bytesWritten], cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			TransitData.Return(transitData);
			writerLock.Release();
		}
	}

	protected virtual ValueTask OnConnectAsync(Socket socket, CancellationToken cancellationToken) => ValueTask.CompletedTask;
	protected virtual void OnSocketEnd(Socket socket) { }

	protected virtual async ValueTask<int> ReadRequestAsync(Socket socket, Memory<byte> requestBuffer, CancellationToken cancellationToken)
	{
		//Read the corresponding 2-byte length in the request to know how long the message is
		await socket.ReceiveAsync(requestBuffer[..2], SocketFlags.None, cancellationToken).ConfigureAwait(false);
		var messageLength = BinaryPrimitives.ReadUInt16BigEndian(requestBuffer.Span);
		//Read the request based on the determined message length
		await socket.ReceiveAsync(requestBuffer[..messageLength], SocketFlags.None, cancellationToken).ConfigureAwait(false);
		return messageLength;
	}

	protected virtual async ValueTask WriteResponseAsync(Socket socket, ReadOnlyMemory<byte> responseBuffer, CancellationToken cancellationToken)
	{
		var tempBuffer = ArrayPool<byte>.Shared.Rent(2);
		try
		{
			//TCP connections require sending a 2-byte length value before the message.
			BinaryPrimitives.WriteUInt16BigEndian(tempBuffer.AsSpan(), (ushort)responseBuffer.Length);
			await socket.SendAsync(tempBuffer.AsMemory(0, 2), SocketFlags.None, cancellationToken).ConfigureAwait(false);
			//Send the response message.
			await socket.SendAsync(responseBuffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(tempBuffer);
		}
	}
}
