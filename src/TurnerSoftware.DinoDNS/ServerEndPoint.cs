using System.Net;
using TurnerSoftware.DinoDNS.Connection;

namespace TurnerSoftware.DinoDNS;

public readonly record struct ServerEndPoint(IPEndPoint EndPoint, IDnsConnectionServer Connection)
{
	public ServerEndPoint(ConnectionType connectionType)
		: this(IPAddress.Any, connectionType) { }

	public ServerEndPoint(IPAddress address, ConnectionType connectionType)
		: this(address, NameServers.GetDefaultPort(connectionType), connectionType) { }

	public ServerEndPoint(IPAddress address, int port, ConnectionType connectionType)
		: this(new IPEndPoint(address, port), connectionType) { }

	public ServerEndPoint(IPEndPoint endPoint, ConnectionType connectionType)
		: this(endPoint, GetDefaultConnectionServer(connectionType)) { }

	public static implicit operator ServerEndPoint(ConnectionType connectionType) => new(connectionType);

	public static IDnsConnectionServer GetDefaultConnectionServer(ConnectionType connectionType) => connectionType switch
	{
		ConnectionType.Udp => UdpConnectionServer.Instance,
		//ConnectionType.Tcp => TcpConnectionServer.Instance,
		//ConnectionType.DoH => HttpsConnectionServer.Instance,
		//ConnectionType.DoT => TlsConnectionServer.Instance,
		_ => throw new NotImplementedException()
	};
}