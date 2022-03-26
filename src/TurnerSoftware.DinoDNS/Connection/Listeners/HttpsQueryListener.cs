using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TurnerSoftware.DinoDNS.Connection.Listeners;

public sealed class HttpsQueryListener : IDnsQueryListener
{
	private readonly X509Certificate2 ServerCertificate;

	public HttpsQueryListener(X509Certificate2 serverCertificate)
	{
		ServerCertificate = serverCertificate;
	}

	public async Task ListenAsync(IPEndPoint endPoint, OnDnsQueryCallback callback, DnsMessageOptions options, CancellationToken cancellationToken)
	{
		var builder = WebApplication.CreateBuilder();
		builder.Logging.ClearProviders();

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