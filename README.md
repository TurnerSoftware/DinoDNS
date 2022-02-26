<div align="center">

![Icon](images/icon.png)
# Dino DNS
A fast and efficient DNS server and client

![Build](https://img.shields.io/github/workflow/status/TurnerSoftware/DinoDNS/Build)
[![NuGet](https://img.shields.io/nuget/v/TurnerSoftware.DinoDNS.svg)](https://www.nuget.org/packages/TurnerSoftware.DinoDNS/)
</div>

## Overview

Dino DNS provides fast and flexible DNS client and server implementations for:

- DNS-over-UDP
- DNS-over-TCP
- [DNS-over-TLS](https://en.wikipedia.org/wiki/DNS-over-TLS)
- [DNS-over-HTTPS](https://en.wikipedia.org/wiki/DNS-over-HTTPS)

## 🤝 Licensing and Support

Dino DNS is licensed under the MIT license. It is free to use in personal and commercial projects.

There are [support plans](https://turnersoftware.com.au/support-plans) available that cover all active [Turner Software OSS projects](https://github.com/TurnerSoftware).
Support plans provide private email support, expert usage advice for our projects, priority bug fixes and more.
These support plans help fund our OSS commitments to provide better software for everyone.

## 🥇 Performance

These performance comparisons show the performance overhead of the DNS library itself and associated allocations.
They do not represent the overhead to remote DNS servers.

The server implementation that each benchmark is performing against is Dino DNS.

### DNS-over-UDP

This is your typical DNS query.
While fast and efficient, it is limited by the lack of transport-layer encryption, reliable delivery and message length.

|            Method |      Mean |    Error |    StdDev |     Op/s | Ratio | RatioSD |   Gen 0 | Allocated |
|------------------ |----------:|---------:|----------:|---------:|------:|--------:|--------:|----------:|
|           DinoDNS |  94.18 us | 1.812 us |  2.420 us | 10,618.3 |  1.00 |    0.00 |  0.4883 |   1,712 B |
|       Kapetan_DNS | 309.60 us | 6.167 us | 13.536 us |  3,230.0 |  3.26 |    0.15 | 23.4375 |  73,996 B |
| MichaCo_DnsClient | 108.00 us | 2.092 us |  2.054 us |  9,259.7 |  1.15 |    0.04 |  1.4648 |   4,664 B |

### DNS-over-TCP

With TCP DNS queries, there is a small overhead from negotiating the connection but otherwise is very fast.
It addresses the reliable delivery and message length limitations that occur with UDP queries.

A good DNS client implementation will pool TCP sockets to avoid needing to negotiate the connection per request.

|            Method |      Mean |    Error |   StdDev |     Op/s | Ratio | RatioSD |  Gen 0 | Allocated |
|------------------ |----------:|---------:|---------:|---------:|------:|--------:|-------:|----------:|
|           DinoDNS |  93.58 us | 1.793 us | 1.678 us | 10,685.9 |  1.00 |    0.00 | 0.4883 |   1,900 B |
| MichaCo_DnsClient | 114.45 us | 2.215 us | 2.551 us |  8,737.3 |  1.23 |    0.03 | 1.4648 |   5,067 B |

<small>
⚠ Note: While Kapetan's DNS client does support TCP, it can't be benchmarked due to port exhaustion issues it has.
</small>

### DNS-over-TLS

With DNS-over-TLS, you get the benefits of DNS-over-TCP with transport-layer encryption between the client and the server.

|  Method |     Mean |   Error |  StdDev |    Op/s | Ratio |  Gen 0 | Allocated |
|-------- |---------:|--------:|--------:|--------:|------:|-------:|----------:|
| DinoDNS | 126.5 us | 2.09 us | 1.95 us | 7,908.1 |  1.00 | 0.4883 |   2,274 B |

👋 Know of a .NET DNS-over-TLS client? Raise a PR to add it as a comparison!

### DNS-over-HTTPS

An alternative to DNS-over-TLS is DNS-over-HTTPS, providing the same core functionality through a different method.
This can disguise DNS traffic when performed over port 443 (the default port for HTTPS).

|  Method |     Mean |   Error |  StdDev |    Op/s | Ratio |  Gen 0 | Allocated |
|-------- |---------:|--------:|--------:|--------:|------:|-------:|----------:|
| DinoDNS | 207.2 us | 3.77 us | 3.52 us | 4,827.1 |  1.00 | 1.4648 |   5,625 B |

👋 Know of a .NET DNS-over-HTTPS client? Raise a PR to add it as a comparison!

## ⭐ Example Usage

```csharp
var client = new DnsClient(new NameServer[]
{
    NameServers.Cloudflare.IPv4.GetPrimary(ConnectionType.Udp)
}, DnsMessageOptions.Default);

var dnsMessage = await client.QueryAsync("example.org", DnsQueryType.A);
var aRecords = dnsMessage.Answers.WithARecords();
```
