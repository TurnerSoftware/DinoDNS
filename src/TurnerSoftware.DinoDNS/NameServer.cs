using System.Net;
using TurnerSoftware.DinoDNS.Connection;

namespace TurnerSoftware.DinoDNS;

public readonly record struct NameServer(IPEndPoint EndPoint, IDnsConnectionClient Connection)
{
	public NameServer(IPAddress address, ConnectionType connectionType)
		: this(address, NameServers.GetDefaultPort(connectionType), connectionType) { }

	public NameServer(IPAddress address, int port, ConnectionType connectionType)
		: this(new IPEndPoint(address, port), connectionType) { }

	public NameServer(IPEndPoint endPoint, ConnectionType connectionType)
		: this(endPoint, GetDefaultConnectionClient(connectionType)) { }

	public static implicit operator NameServer(IPAddress address) => new(address, ConnectionType.Udp);

	public static IDnsConnectionClient GetDefaultConnectionClient(ConnectionType connectionType) => connectionType switch
	{
		ConnectionType.Udp => UdpConnectionClient.Instance,
		ConnectionType.Tcp => TcpConnectionClient.Instance,
		ConnectionType.UdpWithTcpFallback => UdpTcpConnectionClient.Instance,
		ConnectionType.DoH => HttpsConnectionClient.Instance,
		ConnectionType.DoT => TlsConnectionClient.Instance,
		_ => throw new NotImplementedException()
	};
}