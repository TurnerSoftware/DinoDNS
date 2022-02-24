using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TurnerSoftware.DinoDNS.Connection;
using TurnerSoftware.DinoDNS.Protocol;
using TurnerSoftware.DinoDNS.TestServer;

namespace TurnerSoftware.DinoDNS.Tests.Connection;

[TestClass]
public class TlsConnectionTests
{
	public static IDisposable RunTestServer(Func<DnsMessage, DnsMessage> getResponse)
	{
		return DnsTestServer.Instance.Run(new TlsConnectionServer(new System.Net.Security.SslServerAuthenticationOptions
		{
			ServerCertificate = DnsTestServer.CreateTemporaryCertificate()
		}), getResponse);
	}

	[TestMethod]
	public async Task BasicTls()
	{
		using var _ = RunTestServer(request => DnsTestServer.ExampleData.Response);

		var client = new DnsClient(new NameServer[]
		{
			new(DnsTestServer.ClientEndPoint, new TlsConnectionClient(TlsConnectionClient.AuthOptions.DoNotValidate)) 
		}, DnsMessageOptions.Default);

		var response = await client.SendAsync(DnsTestServer.ExampleData.Request);
		Assert.AreEqual(DnsTestServer.ExampleData.Response, response);
	}
}
