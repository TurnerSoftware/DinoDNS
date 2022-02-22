using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;

namespace TurnerSoftware.DinoDNS.Connection;

public sealed class HttpsConnectionClient : IDnsConnectionClient
{
	private const string MimeType = "application/dns-message";

	private static readonly MediaTypeHeaderValue ContentType = new(MimeType);
	private static readonly MediaTypeWithQualityHeaderValue Accept = new(MimeType);

	private static readonly ConcurrentDictionary<IPEndPoint, HttpClient> HttpClients = new();

	public static readonly HttpsConnectionClient Instance = new();

	public async ValueTask<int> SendMessageAsync(IPEndPoint endPoint, ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken)
	{
		var httpClient = HttpClients.GetOrAdd(endPoint, static (endPoint) =>
		{
			var httpClient = new HttpClient
			{
				BaseAddress = new UriBuilder
				{
					Scheme = "https",
					Host = endPoint.Address.ToString(),
					Path = "dns-query"
				}.Uri
			};
			httpClient.DefaultRequestHeaders.Accept.Clear();
			httpClient.DefaultRequestHeaders.Accept.Add(Accept);
			return httpClient;
		});

		var content = new ReadOnlyMemoryContent(sourceBuffer);
		content.Headers.ContentType = ContentType;
		using var response = await httpClient.PostAsync((Uri?)null, content, cancellationToken).ConfigureAwait(false);

		//TODO: Handle response statuses
		var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
		//This is assuming that the stream is a MemoryStream
		return await responseStream.ReadAsync(destinationBuffer, cancellationToken).ConfigureAwait(false);
	}
}

public sealed class HttpsConnectionServer : IDnsConnectionServer
{
	public Task ListenAsync(IPEndPoint endPoint, OnDnsQueryCallback callback, DnsMessageOptions options, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}
}