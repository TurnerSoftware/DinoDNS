using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace TurnerSoftware.DinoDNS.Connection;

public sealed class HttpsResolver : IDnsResolver
{
	private const string MimeType = "application/dns-message";

	private static readonly MediaTypeHeaderValue ContentType = new(MimeType);
	private static readonly MediaTypeWithQualityHeaderValue Accept = new(MimeType);

	public static readonly HttpsResolver Instance = new(HttpConnectionClientOptions.Default);

	private readonly ConcurrentDictionary<IPEndPoint, HttpClient> HttpClients = new();

	private readonly HttpConnectionClientOptions Options;

	public HttpsResolver(HttpConnectionClientOptions options)
	{
		Options = options;
	}

	private HttpClient CreateHttpClient(IPEndPoint endPoint)
	{
		var handler = new HttpClientHandler();
		if (Options.ServerCertificateCustomValidationCallback is not null)
		{
			handler.ClientCertificateOptions = ClientCertificateOption.Manual;
			handler.ServerCertificateCustomValidationCallback = Options.ServerCertificateCustomValidationCallback;
		}

		var httpClient = new HttpClient(handler)
		{
			BaseAddress = new UriBuilder
			{
				Scheme = "https",
				Host = endPoint.Address.ToString(),
				Port = endPoint.Port,
				Path = "dns-query"
			}.Uri,
		};
		httpClient.DefaultRequestHeaders.Accept.Clear();
		httpClient.DefaultRequestHeaders.Accept.Add(Accept);
		return httpClient;
	}

	public async ValueTask<int> SendMessageAsync(IPEndPoint endPoint, ReadOnlyMemory<byte> requestBuffer, Memory<byte> responseBuffer, CancellationToken cancellationToken)
	{
		var httpClient = HttpClients.GetOrAdd(endPoint, CreateHttpClient);
		var content = new ReadOnlyMemoryContent(requestBuffer);
		content.Headers.ContentType = ContentType;
		using var response = await httpClient.PostAsync((Uri?)null, content, cancellationToken).ConfigureAwait(false);

		//TODO: Handle response statuses
		var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
		//This is assuming that the stream is a MemoryStream
		return await responseStream.ReadAsync(responseBuffer, cancellationToken).ConfigureAwait(false);
	}
}

public readonly record struct HttpConnectionClientOptions
{
	public static readonly HttpConnectionClientOptions Default = new();
	public static readonly HttpConnectionClientOptions Insecure = new()
	{
		ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
	};

	public Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> ServerCertificateCustomValidationCallback { get; init; }
}