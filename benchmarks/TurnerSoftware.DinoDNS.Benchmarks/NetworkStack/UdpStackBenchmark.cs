﻿using BenchmarkDotNet.Attributes;
using System.Net;
using TurnerSoftware.DinoDNS.Protocol;

namespace TurnerSoftware.DinoDNS.Benchmarks.NetworkStack;

[Config(typeof(DefaultBenchmarkConfig))]
public class UdpStackBenchmark
{
	private byte[]? RawMessage;

	private DnsClient? DinoDNS_DnsClient;
	private DnsMessage DinoDNS_Message;

	private DNS.Client.DnsClient? Kapetan_DNS_DnsClient;
	private DNS.Client.ClientRequest? Kapetan_DNS_ClientRequest;

	private global::DnsClient.LookupClient? MichaCo_DnsClient_LookupClient;
	private global::DnsClient.DnsQuestion? MichaCo_DnsClient_DnsQuestion;

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

		DinoDNS_DnsClient = new DnsClient(new NameServer[] { new(testEndpoint, ConnectionType.Udp) }, DnsMessageOptions.Default);
		Kapetan_DNS_DnsClient = new DNS.Client.DnsClient(new DNS.Client.RequestResolver.UdpRequestResolver(testEndpoint));
		MichaCo_DnsClient_LookupClient = new global::DnsClient.LookupClient(new global::DnsClient.LookupClientOptions(testEndpoint)
		{
			UseCache = false
		});

		ExternalTestServer.StartUdp();

		DinoDNS_Message = requestMessage;
		Kapetan_DNS_ClientRequest = Kapetan_DNS_DnsClient.FromArray(RawMessage);
		MichaCo_DnsClient_DnsQuestion = new global::DnsClient.DnsQuestion("test.www.example.org", global::DnsClient.QueryType.A);
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		ExternalTestServer.Stop();
	}

	[Benchmark(Baseline = true)]
	public async Task<DnsMessage> DinoDNS()
	{
		return await DinoDNS_DnsClient!.SendAsync(DinoDNS_Message);
	}

	[Benchmark]
	public async Task<DNS.Protocol.IResponse> Kapetan_DNS()
	{
		return await Kapetan_DNS_ClientRequest!.Resolve();
	}

	[Benchmark]
	public async Task<global::DnsClient.IDnsQueryResponse> MichaCo_DnsClient()
	{
		return await MichaCo_DnsClient_LookupClient!.QueryAsync(MichaCo_DnsClient_DnsQuestion);
	}
}
