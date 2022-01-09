using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using System.Diagnostics;
using System.Net;
using TurnerSoftware.DinoDNS.Messengers;
using TurnerSoftware.DinoDNS.Protocol;

namespace TurnerSoftware.DinoDNS.Benchmarks;

[Config(typeof(CustomConfig))]
[SimpleJob(RuntimeMoniker.Net60)]
public class FullStackBenchmark
{
	private Process? TestServerProcess;
	private byte[]? RawMessage;

	private DnsClient? DinoDNS_DnsClient;
	private DnsMessage DinoDNS_Message;

	private DNS.Client.DnsClient? Kapetan_DNS_DnsClient;
	private DNS.Client.ClientRequest? Kapetan_DNS_ClientRequest;

	[GlobalSetup]
	public void Setup()
	{
		var requestMessage = DnsMessage.CreateQuery(44124)
			.WithQuestions(new Question[]
			{
				new()
				{
					Query = new LabelSequence("test.www.example.org"),
					Type = DnsQueryType.A,
					Class = DnsClass.IN
				}
			});

		var buffer = new byte[1024];
		var messageBytes = new DnsProtocolWriter(buffer.AsMemory())
			.AppendMessage(requestMessage)
			.GetWrittenBytes()
			.ToArray();

		RawMessage = messageBytes;

		var testEndpoint = new IPEndPoint(new IPAddress(new byte[] { 127, 0, 0, 1 }), 53);

		DinoDNS_DnsClient = new DnsClient(new IDnsMessenger[] { new UdpMessenger(testEndpoint) }, DnsClientOptions.Default);
		DinoDNS_Message = requestMessage;

		Kapetan_DNS_DnsClient = new DNS.Client.DnsClient(testEndpoint);
		Kapetan_DNS_ClientRequest = Kapetan_DNS_DnsClient.FromArray(RawMessage);

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
	public async Task<DnsMessage> DinoDNS()
	{
		return await DinoDNS_DnsClient!.SendAsync(DinoDNS_Message);
	}

	//[Benchmark]
	//public async Task<DNS.Protocol.IResponse> Kapetan_DNS()
	//{
	//	return await Kapetan_DNS_ClientRequest!.Resolve();
	//}
}
