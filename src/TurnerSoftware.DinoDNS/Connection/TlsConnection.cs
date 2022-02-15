using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System;
using System.Net.Security;

namespace TurnerSoftware.DinoDNS.Connection;

public sealed class TlsConnection : IDnsConnection
{
	private static readonly ConcurrentDictionary<IPEndPoint, ConcurrentQueue<Socket>> Sockets = new();

	public static readonly TlsConnection Instance = new();

	public async ValueTask<int> SendMessageAsync(IPEndPoint endPoint, ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken)
	{
		static ConcurrentQueue<Socket> InitialiseSocketQueue(IPEndPoint endPoint) => new();
		var socketQueue = Sockets.GetOrAdd(endPoint, InitialiseSocketQueue);

		static Socket NewSocket(IPEndPoint endPoint)
		{
			//TODO: Configure Send/Receive Timeout for socket
			var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
			{
				SendBufferSize = 2048,
				ReceiveBufferSize = 2048,
				SendTimeout = 10,
				ReceiveTimeout = 10
			};
			return socket;
		}
		if (socketQueue.TryDequeue(out var socket))
		{
			if (!socket.Connected)
			{
				//TODO: Investigate whether we can just re-connect to existing sockets that are closed
				socket.Dispose();
				socket = NewSocket(endPoint);
			}
		}
		else if (socket is null)
		{
			socket = NewSocket(endPoint);
		}

		try
		{
			if (!socket.Connected)
			{
				await socket.ConnectAsync(endPoint).ConfigureAwait(false);
			}

			using var tlsStream = new SslStream(new NetworkStream(socket), true);
			await tlsStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
			{
				TargetHost = endPoint.Address.ToString(),
				EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
			}, cancellationToken);

			//TCP connections require sending a 2-byte length value before the message.
			//Use our destination buffer as a temporary buffer to get and send the length.
			BinaryPrimitives.WriteUInt16BigEndian(destinationBuffer.Span, (ushort)sourceBuffer.Length);
			await tlsStream.WriteAsync(destinationBuffer[..2], cancellationToken).ConfigureAwait(false);
			//Send our main message from our source buffer	
			await tlsStream.WriteAsync(sourceBuffer, cancellationToken).ConfigureAwait(false);

			//Read the corresponding 2-byte length in the response to know how long the message is
			await tlsStream.ReadAsync(destinationBuffer[..2], cancellationToken).ConfigureAwait(false);
			var messageLength = BinaryPrimitives.ReadUInt16BigEndian(destinationBuffer.Span);
			//Read the response based on the determined message length
			await tlsStream.ReadAsync(destinationBuffer[..messageLength], cancellationToken).ConfigureAwait(false);
			return messageLength;
		}
		finally
		{
			socketQueue.Enqueue(socket);
		}
	}
}
