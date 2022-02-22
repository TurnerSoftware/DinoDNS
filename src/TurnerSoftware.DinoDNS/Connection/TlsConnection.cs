using System.Buffers.Binary;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Collections.Concurrent;
using System.Net;

namespace TurnerSoftware.DinoDNS.Connection;

public sealed class TlsConnectionClient : TcpConnectionClient
{
	public const SslProtocols DefaultSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
	public new static readonly TlsConnectionClient Instance = new(DefaultSslProtocols);

	public readonly SslProtocols EnabledSslProtocols;

	private readonly ConcurrentDictionary<IntPtr, SslStream> StreamLookup = new();

	public TlsConnectionClient(SslProtocols enabledSslProtocols)
	{
		EnabledSslProtocols = enabledSslProtocols;
	}

	protected override async ValueTask OnConnectAsync(Socket socket, IPEndPoint endPoint, CancellationToken cancellationToken)
	{
		var stream = new SslStream(new NetworkStream(socket), true);
		await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
		{
			TargetHost = socket.RemoteEndPoint!.ToString(),
			EnabledSslProtocols = EnabledSslProtocols
		}, cancellationToken).ConfigureAwait(false);
		StreamLookup.TryAdd(socket.Handle, stream);
	}

	protected override void OnSocketEnd(Socket socket) => StreamLookup.TryRemove(socket.Handle, out _);

	protected override async ValueTask<int> PerformQueryAsync(Socket socket, ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken)
	{
		var stream = StreamLookup.GetValueOrDefault(socket.Handle)!;

		//TCP connections require sending a 2-byte length value before the message.
		//Use our destination buffer as a temporary buffer to get and send the length.
		BinaryPrimitives.WriteUInt16BigEndian(destinationBuffer.Span, (ushort)sourceBuffer.Length);
		await stream.WriteAsync(destinationBuffer[..2], cancellationToken).ConfigureAwait(false);
		//Send our main message from our source buffer	
		await stream.WriteAsync(sourceBuffer, cancellationToken).ConfigureAwait(false);

		//Read the corresponding 2-byte length in the response to know how long the message is
		await stream.ReadAsync(destinationBuffer[..2], cancellationToken).ConfigureAwait(false);
		var messageLength = BinaryPrimitives.ReadUInt16BigEndian(destinationBuffer.Span);
		//Read the response based on the determined message length
		await stream.ReadAsync(destinationBuffer[..messageLength], cancellationToken).ConfigureAwait(false);

		return messageLength;
	}
}

public sealed class TlsConnectionServer : TcpConnectionServer
{

}