using System.Net;
using IPNetwork = System.Net.IPNetwork;
using Microsoft.AspNetCore.HttpOverrides;

namespace ControlR.Web.Server.Startup;

public static class ForwardedHeadersRegistrationExtensions
{
  /// <summary>
  /// The forwarded headers ControlR consumes. A forwarded host and a forwarded path prefix are
  /// deliberately excluded.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Emailed links are built on this server's authority but inherited their origin from the request, so
  /// an adopted <c>X-Forwarded-Host</c> let a caller aim a genuine password-reset email carrying a valid
  /// token at an origin they control. Cloudflare passes a client-supplied <c>X-Forwarded-Host</c> through
  /// to the origin rather than overwriting it, and <c>ForwardedHeaders.All</c> adopted the value
  /// verbatim.
  /// </para>
  /// <para>
  /// The raw <c>Host</c> header carries everything ControlR needs. Proxies that rewrite it (Cloudflare,
  /// Caddy, nginx with <c>proxy_set_header Host $host</c>) already deliver the public hostname. A
  /// deployment behind a balancer that only ever sets <c>X-Forwarded-Host</c> and leaves <c>Host</c> at
  /// the upstream address must pin <c>AllowedHosts</c> and set <c>AppOptions:PublicBaseUrl</c>, which is
  /// what emailed links use in preference to the request.
  /// </para>
  /// </remarks>
  private const ForwardedHeaders TrustedForwardedHeaders =
    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

  public static async Task AddControlrForwardedHeaders(
    this IHostApplicationBuilder hostBuilder,
    AppOptions appOptions)
  {
    if (appOptions.EnableNetworkTrust)
    {
      hostBuilder.Services.Configure<ForwardedHeadersOptions>(options =>
      {
        options.ForwardedHeaders = TrustedForwardedHeaders;
        options.ForwardLimit = null;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
      });
      return;
    }

    var cloudflareIps = new List<IPNetwork>();

    if (appOptions.EnableCloudflareProxySupport)
    {
      using var httpClient = new HttpClient();
      using var ip4Response = await httpClient.GetAsync("https://www.cloudflare.com/ips-v4");
      ip4Response.EnsureSuccessStatusCode();
      var ip4Content = await ip4Response.Content.ReadAsStringAsync();
      var ip4Networks = ip4Content.Split();

      using var ip6Response = await httpClient.GetAsync("https://www.cloudflare.com/ips-v6");
      ip6Response.EnsureSuccessStatusCode();
      var ip6Content = await ip4Response.Content.ReadAsStringAsync();
      var ip6Networks = ip6Content.Split();

      string[] ipNetworks = [.. ip4Networks, .. ip6Networks];

      foreach (var network in ipNetworks)
      {
        if (!IPNetwork.TryParse(network, out var ipNetwork))
        {
          Console.WriteLine($"Invalid Cloudflare network: {network}");
        }
        else
        {
          Console.WriteLine($"Adding Cloudflare KnownNetwork: {network}");
          cloudflareIps.Add(ipNetwork);
        }
      }
    }

    hostBuilder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
      options.ForwardedHeaders = TrustedForwardedHeaders;
      options.ForwardLimit = null;

      // Default Docker host. We want to allow forwarded headers from this address.
      if (!string.IsNullOrWhiteSpace(appOptions.DockerGatewayIp))
      {
        if (IPAddress.TryParse(appOptions.DockerGatewayIp, out var dockerGatewayIp))
        {
          options.KnownProxies.Add(dockerGatewayIp);
        }
        else
        {
          Console.WriteLine($"Invalid DockerGatewayIp: {appOptions.DockerGatewayIp}");
        }
      }

      if (appOptions.KnownProxies is { Length: > 0 } knownProxies)
      {
        foreach (var proxy in knownProxies)
        {
          if (IPAddress.TryParse(proxy, out var ip))
          {
            Console.WriteLine($"Adding KnownProxy: {proxy}");
            options.KnownProxies.Add(ip);
          }
          else
          {
            Console.WriteLine($"Invalid KnownProxy IP: {proxy}");
          }
        }
      }

      if (appOptions.KnownNetworks is { Length: > 0 } knownNetworks)
      {
        foreach (var network in knownNetworks)
        {
          if (IPNetwork.TryParse(network, out var ipNetwork))
          {
            Console.WriteLine($"Adding KnownNetwork: {network}");
            options.KnownIPNetworks.Add(ipNetwork);
          }
          else
          {
            Console.WriteLine($"Invalid KnownNetwork: {network}");
          }
        }
      }

      if (cloudflareIps.Count > 0)
      {
        foreach (var cloudflareIp in cloudflareIps)
        {
          options.KnownIPNetworks.Add(cloudflareIp);
        }
      }
    });
  }
}