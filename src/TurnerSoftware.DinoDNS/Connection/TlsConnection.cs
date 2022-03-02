using System.Buffers.Binary;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Collections.Concurrent;
using System.Net;
using System.Buffers;

namespace TurnerSoftware.DinoDNS.Connection;

public sealed class TlsConnectionClient : TcpConnectionClient
{
	public sealed class AuthOptions
	{
		public static readonly Func<EndPoint, SslClientAuthenticationOptions> Default = static _ => new();

		public static readonly Func<EndPoint, SslClientAuthenticationOptions> DoNotValidate = static _ => new()
		{
			RemoteCertificateValidationCallback = (_, _, _, _) => true
		};
	}

	public new static readonly TlsConnectionClient Instance = new(AuthOptions.Default);

	private readonly Func<EndPoint, SslClientAuthenticationOptions> AuthOptionsFactory;

	private readonly ConcurrentDictionary<IntPtr, SslStream> StreamLookup = new();

	public TlsConnectionClient(Func<EndPoint, SslClientAuthenticationOptions> authOptionsFactory)
	{
		AuthOptionsFactory = authOptionsFactory;
	}

	protected override async ValueTask OnConnectAsync(Socket socket, IPEndPoint endPoint, CancellationToken cancellationToken)
	{
		var stream = new SslStream(new NetworkStream(socket), true);
		var socketSpecificAuthOptions = AuthOptionsFactory(socket.RemoteEndPoint!);
		if (socketSpecificAuthOptions.TargetHost is null)
		{
			socketSpecificAuthOptions.TargetHost = endPoint.ToString();
		}
		await stream.AuthenticateAsClientAsync(socketSpecificAuthOptions, cancellationToken).ConfigureAwait(false);
		StreamLookup.TryAdd(socket.Handle, stream);
	}

	protected override void OnSocketEnd(Socket socket) => StreamLookup.TryRemove(socket.Handle, out _);

	protected override async ValueTask<int> PerformQueryAsync(Socket socket, ReadOnlyMemory<byte> requestBuffer, Memory<byte> responseBuffer, CancellationToken cancellationToken)
	{
		var stream = StreamLookup.GetValueOrDefault(socket.Handle)!;

		//TCP connections require sending a 2-byte length value before the message.
		//Use our destination buffer as a temporary buffer to get and send the length.
		BinaryPrimitives.WriteUInt16BigEndian(responseBuffer.Span, (ushort)requestBuffer.Length);
		await stream.WriteAsync(responseBuffer[..2], cancellationToken).ConfigureAwait(false);
		//Send our main message from our source buffer	
		await stream.WriteAsync(requestBuffer, cancellationToken).ConfigureAwait(false);

		//Read the corresponding 2-byte length in the response to know how long the message is
		await stream.ReadAsync(responseBuffer[..2], cancellationToken).ConfigureAwait(false);
		var messageLength = BinaryPrimitives.ReadUInt16BigEndian(responseBuffer.Span);
		//Read the response based on the determined message length
		await stream.ReadAsync(responseBuffer[..messageLength], cancellationToken).ConfigureAwait(false);

		return messageLength;
	}
}

public sealed class TlsConnectionServer : TcpConnectionServer
{
	public readonly SslProtocols EnabledSslProtocols;

	private readonly ConcurrentDictionary<IntPtr, SslStream> StreamLookup = new();
	private readonly SslServerAuthenticationOptions Options;

	public TlsConnectionServer(SslServerAuthenticationOptions options)
	{
		Options = options;
	}

	protected override async ValueTask OnConnectAsync(Socket socket, CancellationToken cancellationToken)
	{
		var stream = new SslStream(new NetworkStream(socket), true);
		await stream.AuthenticateAsServerAsync(Options, cancellationToken).ConfigureAwait(false);
		StreamLookup.TryAdd(socket.Handle, stream);
	}

	protected override void OnSocketEnd(Socket socket) => StreamLookup.TryRemove(socket.Handle, out _);

	protected override async ValueTask<int> ReadRequestAsync(Socket socket, Memory<byte> requestBuffer, CancellationToken cancellationToken)
	{
		var stream = StreamLookup.GetValueOrDefault(socket.Handle)!;
		//Read the corresponding 2-byte length in the request to know how long the message is
		await stream.ReadAsync(requestBuffer[..2], cancellationToken).ConfigureAwait(false);
		var messageLength = BinaryPrimitives.ReadUInt16BigEndian(requestBuffer.Span);
		//Read the request based on the determined message length
		await stream.ReadAsync(requestBuffer[..messageLength], cancellationToken).ConfigureAwait(false);
		return messageLength;
	}

	protected override async ValueTask WriteResponseAsync(Socket socket, ReadOnlyMemory<byte> responseBuffer, CancellationToken cancellationToken)
	{
		var tempBuffer = ArrayPool<byte>.Shared.Rent(2);
		try
		{
			var stream = StreamLookup.GetValueOrDefault(socket.Handle)!;
			//TCP connections require sending a 2-byte length value before the message.
			BinaryPrimitives.WriteUInt16BigEndian(tempBuffer.AsSpan(), (ushort)responseBuffer.Length);
			await stream.WriteAsync(tempBuffer.AsMemory(0, 2), cancellationToken).ConfigureAwait(false);
			//Send the response message.
			await stream.WriteAsync(responseBuffer, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(tempBuffer);
		}
	}
}