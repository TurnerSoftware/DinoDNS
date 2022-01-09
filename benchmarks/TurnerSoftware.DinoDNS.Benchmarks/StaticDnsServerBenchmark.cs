using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace TurnerSoftware.DinoDNS.Benchmarks;

[Config(typeof(CustomConfig))]
[SimpleJob(RuntimeMoniker.Net60)]
public class StaticDnsServerBenchmark
{
	private UdpClient? UdpClient;
	private Process? TestServerProcess;

	private readonly IPEndPoint EndPoint = new(new IPAddress(new byte[] { 127, 0, 0, 1 }), 53);
	private readonly byte[] Source = new byte[1] { 0 };
	private readonly byte[] Destination = new byte[1024];

	[GlobalSetup]
	public void Setup()
	{
		UdpClient = new UdpClient();
		TestServerProcess = Process.Start(new ProcessStartInfo("StaticDnsServer.exe"));
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		if (TestServerProcess is not null && !TestServerProcess.HasExited)
		{
			TestServerProcess.Kill();
		}
	}

	[Benchmark(Baseline = true)]
	public void StaticDnsServer()
	{
		UdpClient!.Send(Source, EndPoint);
		UdpClient.Client.Receive(Destination!);
	}
}
