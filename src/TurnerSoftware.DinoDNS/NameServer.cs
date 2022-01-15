using System.Net;

namespace TurnerSoftware.DinoDNS;

public readonly record struct NameServer(IPEndPoint EndPoint, ConnectionType ConnectionType)
{
	public NameServer(IPAddress address, ConnectionType connectionType)
		: this(address, NameServers.GetDefaultPort(connectionType), connectionType) { }

	public NameServer(IPAddress address, int port, ConnectionType connectionType)
		: this(new IPEndPoint(address, port), connectionType) { }

	public static implicit operator NameServer(IPAddress address) => new(address, ConnectionType.Udp);
}