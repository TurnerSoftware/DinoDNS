﻿using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TurnerSoftware.DinoDNS.Connection;
using TurnerSoftware.DinoDNS.Protocol;
using TurnerSoftware.DinoDNS.TestServer;

namespace TurnerSoftware.DinoDNS.Tests.Connection;

[TestClass]
public class UdpConnectionTests
{
	public static IAsyncDisposable RunTestServer(Func<DnsMessage, DnsMessage> getResponse)
	{
		return DnsTestServer.Instance.Run(new UdpConnectionServer(), getResponse);
	}

	[TestMethod]
	public async Task BasicTcp()
	{
		await using var _ = RunTestServer(request => DnsTestServer.ExampleData.Response);

		var client = new DnsClient(new NameServer[]
		{
			new(DnsTestServer.ClientEndPoint, new UdpConnectionClient()) 
		}, DnsMessageOptions.Default);

		var response = await client.SendAsync(DnsTestServer.ExampleData.Request);
		Assert.AreEqual(DnsTestServer.ExampleData.Response, response);
	}
}
