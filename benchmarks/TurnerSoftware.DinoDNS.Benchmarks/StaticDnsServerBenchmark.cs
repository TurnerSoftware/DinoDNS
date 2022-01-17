using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Net;
using System.Net.Sockets;

namespace TurnerSoftware.DinoDNS.Benchmarks;

[Config(typeof(DefaultBenchmarkConfig))]
public class StaticDnsServerBenchmark
{
	private Socket? Socket;

	private readonly IPEndPoint EndPoint = new(new IPAddress(new byte[] { 127, 0, 0, 1 }), 53);
	private readonly byte[] Source = new byte[1] { 0 };
	private readonly byte[] Destination = new byte[1024];

	[GlobalSetup]
	public void Setup()
	{
		Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		ExternalTestServer.StartUdp();
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		ExternalTestServer.Stop();
	}

	[Benchmark(Baseline = true)]
	public void StaticDnsServer()
	{
		Socket!.SendTo(Source, EndPoint);
		Socket.Receive(Destination);
	}
}
