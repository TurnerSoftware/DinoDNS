using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TurnerSoftware.DinoDNS.Tests;

[TestClass]
public class DnsHostsReaderTests
{
	private static void CheckToken(ref DnsHostsReader reader, string expected, HostsTokenType tokenType)
	{
		Assert.IsTrue(reader.NextToken(out var token), "No token");
		Assert.AreEqual(tokenType, token.TokenType, $"Token-mismatch for {expected}");
		Assert.AreEqual(expected, token.Value.ToString());
	}

	[DataTestMethod]
	[DataRow("# This is a comment", HostsTokenType.Comment, DisplayName = "Comment")]
	[DataRow("\r", HostsTokenType.NewLine, DisplayName = "Carriage Return")]
	[DataRow("\n", HostsTokenType.NewLine, DisplayName = "New Line")]
	[DataRow("\r\n", HostsTokenType.NewLine, DisplayName = "Carriage Return + New Line")]
	[DataRow(" ", HostsTokenType.Whitespace, DisplayName = "Space")]
	[DataRow("\t", HostsTokenType.Whitespace, DisplayName = "Tab")]
	[DataRow("127.0.0.1", HostsTokenType.HostOrAddress, DisplayName = "Address")]
	[DataRow("example.org", HostsTokenType.HostOrAddress, DisplayName = "Host")]
	public void ReadSingleToken(string hostsFile, HostsTokenType tokenType)
	{
		var reader = new DnsHostsReader(hostsFile);
		CheckToken(ref reader, hostsFile, tokenType);
	}

	[TestMethod]
	public void ReadHostsFile()
	{
		var hostsFile = @"# This is an example hosts file
127.0.0.1 localhost
192.168.0.1	gateway router

1.1.1.1 cloudflare # This is cloudflare";

		var reader = new DnsHostsReader(hostsFile);
		CheckToken(ref reader, "# This is an example hosts file", HostsTokenType.Comment);
		CheckToken(ref reader, "\r\n", HostsTokenType.NewLine);
		CheckToken(ref reader, "127.0.0.1", HostsTokenType.HostOrAddress);
		CheckToken(ref reader, " ", HostsTokenType.Whitespace);
		CheckToken(ref reader, "localhost", HostsTokenType.HostOrAddress);
		CheckToken(ref reader, "\r\n", HostsTokenType.NewLine);
		CheckToken(ref reader, "192.168.0.1", HostsTokenType.HostOrAddress);
		CheckToken(ref reader, "\t", HostsTokenType.Whitespace);
		CheckToken(ref reader, "gateway", HostsTokenType.HostOrAddress);
		CheckToken(ref reader, " ", HostsTokenType.Whitespace);
		CheckToken(ref reader, "router", HostsTokenType.HostOrAddress);
		CheckToken(ref reader, "\r\n", HostsTokenType.NewLine);
		CheckToken(ref reader, "\r\n", HostsTokenType.NewLine);
		CheckToken(ref reader, "1.1.1.1", HostsTokenType.HostOrAddress);
		CheckToken(ref reader, " ", HostsTokenType.Whitespace);
		CheckToken(ref reader, "cloudflare", HostsTokenType.HostOrAddress);
		CheckToken(ref reader, " ", HostsTokenType.Whitespace);
		CheckToken(ref reader, "# This is cloudflare", HostsTokenType.Comment);
		Assert.IsFalse(reader.NextToken(out var token), $"Found token {token.Value}");
	}
}
