﻿using TurnerSoftware.DinoDNS;
using TurnerSoftware.DinoDNS.Protocol;
using System.Net.Sockets;
using System.Text;

Console.WriteLine(@"QDCOUNT: Question record count
ANCOUNT: Answer record count
NSCOUNT: Authority record count
ARCOUNT: Additional record count
=====");

// Support updates: https://datatracker.ietf.org/doc/html/rfc2136
// Core RFC for DNS: http://www.networksorcery.com/enp/rfc/rfc1035.txt
// https://datatracker.ietf.org/doc/html/rfc1035


//ShowDecode();

await RunClientAsync();

static void ShowDecode()
{
	var validBytes = new byte[]
	{
		0xc0, 0x0c, 0x00, 0x05, 0x00, 0x01, 0x00, 0x00,
		0x04, 0x9a, 0x00, 0x27, 0x06, 0x61, 0x73, 0x69,
		0x6d, 0x6f, 0x76, 0x06, 0x76, 0x6f, 0x72, 0x74,
		0x65, 0x78, 0x04, 0x64, 0x61, 0x74, 0x61, 0x0e,
		0x74, 0x72, 0x61, 0x66, 0x66, 0x69, 0x63, 0x6d,
		0x61, 0x6e, 0x61, 0x67, 0x65, 0x72, 0x03, 0x6e,
		0x65, 0x74, 0x00
	};
	var myBytes = new byte[]
	{
		0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00,
		0x00, 0x00, 0x00, 0x00, 0x06, 0x76, 0x6f, 0x72,
		0x74, 0x65, 0x78, 0x04, 0x64, 0x61, 0x74, 0x61,
		0x09, 0x6d, 0x69, 0x63, 0x72, 0x6f, 0x73, 0x6f,
		0x66, 0x74, 0x03, 0x63, 0x6f, 0x6d, 0x00, 0x01,
		0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
	};
	var testBytes = new byte[]
	{
		0x75, 0xea, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00,
		0x00, 0x00, 0x00, 0x00, 0x06, 0x76, 0x6f, 0x72,
		0x74, 0x65, 0x78, 0x04, 0x64, 0x61, 0x74, 0x61,
		0x09, 0x6d, 0x69, 0x63, 0x72, 0x6f, 0x73, 0x6f,
		0x66, 0x74, 0x03, 0x63, 0x6f, 0x6d, 0x00, 0x01,
		0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
	};

	Console.WriteLine("Theirs");
	ProcessRequest(validBytes);
	Console.WriteLine("Mine");
	ProcessRequest(myBytes);
	//Console.WriteLine("Test");
	//ProcessRequest(testBytes);
}
static async ValueTask RunClientAsync()
{
	using var remoteServer = new UdpClient();
	remoteServer.Connect("192.168.0.11", 53);
	//remoteServer.Connect("1.1.1.1", 53);

	byte[] buffer = new byte[1024];
	WriteClientTestData(buffer);

	//Console.WriteLine("Request");
	//WriteBytes(buffer.AsSpan()[..50]);

	await remoteServer.SendAsync(buffer);
	var remoteResult = await remoteServer.ReceiveAsync();
	ProcessResponse(remoteResult.Buffer);


	static void WriteClientTestData(Span<byte> buffer)
	{
		var header = new Header
		{
			Identification = 1,
			Flags = new HeaderFlags
			{
				QueryOrResponse = QueryOrResponse.Query,
				RecursionDesired = RecursionDesired.Yes
			},
			QuestionRecordCount = 1,
		};
		header.WriteTo(buffer);

		var question = new Question(LabelSequence.Parse("vortex.data.microsoft.com"), DnsQueryType.A, DnsClass.IN);
		question.WriteTo(buffer[12..], out _);
	}
}

static async ValueTask RunServerAsync()
{
	using var localClient = new UdpClient(53);
	using var remoteServer = new UdpClient();
	remoteServer.Connect("192.168.0.10", 53);
	while (true)
	{
		var result = await localClient.ReceiveAsync();
		ProcessRequest(result.Buffer);

		await remoteServer.SendAsync(result.Buffer);
		var remoteResult = await remoteServer.ReceiveAsync();
		ProcessResponse(remoteResult.Buffer);

		await localClient.SendAsync(remoteResult.Buffer, result.RemoteEndPoint);
	}
}

static void ProcessRequest(byte[] buffer)
{
	ReadOnlySpan<byte> span = buffer.AsSpan();
	var header = new Header(span);

	Console.WriteLine(header.ToString());

	span = span[12..];
	for (var i = 0; i < header.QuestionRecordCount; i++)
	{
		var question = Question.Parse(span, out var questionLength);
		Console.WriteLine(question.ToString());
		span = span[questionLength..];
	}
}

static void ProcessResponse(byte[] buffer)
{
	SeekableReadOnlySpan<byte> seekableSpan = buffer.AsSpan();

	var header = new Header(seekableSpan);

	Console.WriteLine(header.ToString());

	seekableSpan += Header.Length;

	//Console.WriteLine("Response");
	//WriteBytes(seekableSpan.Span);

	for (var i = 0; i < header.QuestionRecordCount; i++)
	{
		var question = Question.Parse(seekableSpan, out var questionLength);
		Console.WriteLine(question.ToString());
		seekableSpan += questionLength;
	}

	for (var i = 0; i < header.AnswerRecordCount; i++)
	{
		var answer = ResourceRecord.Parse(seekableSpan, out var answerLength);
		Console.WriteLine(answer.ToString());
		seekableSpan += answerLength;
	}
}

static void WriteBytes(ReadOnlySpan<byte> value)
{
	for (var i = 0; i < value.Length; i++)
	{
		WriteByte(value[i]);

		if (i + 1 < value.Length)
		{
			Console.Write(' ');
			WriteByte(value[++i]);
		}

		Console.WriteLine();
	}
}

static void WriteByte(byte value)
{
	Console.Write(Convert.ToString(value, 2).PadLeft(8, '0'));
	Console.Write(" [");
	Console.Write(Convert.ToString(value, 10).PadLeft(5, ' '));
	Console.Write('/');
	Console.Write((char)value);
	Console.Write(']');
}