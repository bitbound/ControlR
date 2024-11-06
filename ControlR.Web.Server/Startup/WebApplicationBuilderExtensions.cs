using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace ControlR.Web.Server.Startup;

public static class WebApplicationBuilderExtensions
{
  public static async Task<WebApplicationBuilder> ConfigureForwardedHeaders(
    this WebApplicationBuilder builder,
    AppOptions appOptions)
  {
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

      string[] ipNetworks = [..ip4Networks, ..ip6Networks];

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

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
      options.ForwardedHeaders = ForwardedHeaders.All;
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
            Console.WriteLine("Invalid KnownProxy IP: {proxy}");
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
            options.KnownNetworks.Add(ipNetwork);
          }
          else
          {
            Console.WriteLine("Invalid KnownNetwork: {network}");
          }
        }
      }

      if (cloudflareIps.Count > 0)
      {
        foreach (var cloudflareIp in cloudflareIps)
        {
          options.KnownNetworks.Add(cloudflareIp);
        }
      }
    });
    
    return builder;
  }
}