using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TurnerSoftware.DinoDNS.Connection;

public sealed class HttpsConnectionClient : IDnsConnectionClient
{
	private const string MimeType = "application/dns-message";

	private static readonly MediaTypeHeaderValue ContentType = new(MimeType);
	private static readonly MediaTypeWithQualityHeaderValue Accept = new(MimeType);

	public static readonly HttpsConnectionClient Instance = new(HttpConnectionClientOptions.Default);

	private readonly ConcurrentDictionary<IPEndPoint, HttpClient> HttpClients = new();

	private readonly HttpConnectionClientOptions Options;

	public HttpsConnectionClient(HttpConnectionClientOptions options)
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

	public async ValueTask<int> SendMessageAsync(IPEndPoint endPoint, ReadOnlyMemory<byte> sourceBuffer, Memory<byte> destinationBuffer, CancellationToken cancellationToken)
	{
		var httpClient = HttpClients.GetOrAdd(endPoint, CreateHttpClient);
		var content = new ReadOnlyMemoryContent(sourceBuffer);
		content.Headers.ContentType = ContentType;
		using var response = await httpClient.PostAsync((Uri?)null, content, cancellationToken).ConfigureAwait(false);

		//TODO: Handle response statuses
		var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
		//This is assuming that the stream is a MemoryStream
		return await responseStream.ReadAsync(destinationBuffer, cancellationToken).ConfigureAwait(false);
	}
}

public readonly record struct HttpConnectionClientOptions
{
	public static readonly HttpConnectionClientOptions Default = new();
	public static readonly HttpConnectionClientOptions Insecure = new()
	{
		ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
	};

	public Func<HttpRequestMessage,X509Certificate2?,X509Chain?,SslPolicyErrors,bool> ServerCertificateCustomValidationCallback { get; init; }
}

public sealed class HttpsConnectionServer : IDnsConnectionServer
{
	private readonly X509Certificate2 ServerCertificate;

	public HttpsConnectionServer(X509Certificate2 serverCertificate)
	{
		ServerCertificate = serverCertificate;
	}

	public async Task ListenAsync(IPEndPoint endPoint, OnDnsQueryCallback callback, DnsMessageOptions options, CancellationToken cancellationToken)
	{
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddSingleton(callback);
		builder.WebHost.ConfigureKestrel(options =>
		{
			options.ConfigureHttpsDefaults(httpsOptions =>
			{
				httpsOptions.ServerCertificate = ServerCertificate;
			});
		});
		
		var app = builder.Build();
		app.Urls.Add($"https://{endPoint}/");
		app.MapPost("/dns-query", async context =>
		{
			//DNS query is larger than maximum allowed DNS message size.
			if (context.Request.ContentLength > options.MaximumMessageSize)
			{
				context.Response.StatusCode = 413;
				return;
			}

			var transitData = TransitData.Rent(options);
			var (requestBuffer, responseBuffer) = transitData;
			try
			{
				//TODO: Handle other types of status codes

				await context.Request.Body.ReadAsync(transitData.RequestBuffer, cancellationToken).ConfigureAwait(false);
				var bytesWritten = await callback(requestBuffer, responseBuffer, cancellationToken).ConfigureAwait(false);

				context.Response.StatusCode = 200;
				context.Response.ContentType = "application/dns-message";
				context.Response.ContentLength = bytesWritten;
				await context.Response.Body.WriteAsync(responseBuffer[..bytesWritten], cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				TransitData.Return(transitData);
			}
		});

		await app.RunAsync(cancellationToken).ConfigureAwait(false);
	}
}