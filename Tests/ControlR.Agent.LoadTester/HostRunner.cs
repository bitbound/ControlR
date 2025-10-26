using System.Collections.Concurrent;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Models;
using ControlR.Agent.Common.Services;
using ControlR.Agent.Common.Startup;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Hubs;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Signalr.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using ControlR.Agent.LoadTester.Helpers;

namespace ControlR.Agent.LoadTester;
public static class HostRunner
{
  public static async Task Run(string[] args)
  {

    var agentCount = ArgsParser.GetArgValue<int>("--agent-count");
    var startCount = ArgsParser.GetArgValue<int>("--start-count");
    var serverUriArg = ArgsParser.GetArgValue<string>("--server-uri");
    var serverUri = new Uri(serverUriArg);
    var agentVersion = await GetAgentVersion(serverUri);
    var tenantId = Guid.Empty;

    Console.WriteLine($"Starting agent count at {startCount}.");

    using var cts = new CancellationTokenSource();
    var cancellationToken = cts.Token;

    Console.CancelKeyPress += (s, e) => { cts.Cancel(); };

    Console.WriteLine($"Connecting to {serverUri}");

    var hosts = new ConcurrentBag<IHost>();

    _ = ReportHosts(hosts, cancellationToken);

    await Parallel.ForAsync(startCount, startCount + agentCount, async (i, ct) =>
    {
      if (ct.IsCancellationRequested)
      {
        return;
      }

      var builder = Host.CreateApplicationBuilder(args);
      builder.AddControlRAgent(StartupMode.Run, $"loadtester-{i}", serverUri);

      var deviceId = DeterministicGuid.Create(i);

      builder.Configuration
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
          { "AppOptions:DeviceId", deviceId.ToString() },
          { "AppOptions:ServerUri", $"{serverUri}" },
          { "AppOptions:TenantId", tenantId.ToString() },
          { "Logging:LogLevel:Default", "Warning" },
          { "Serilog:MinimumLevel:Default", "Warning" }
        });

      builder.Services.ReplaceService<IAgentUpdater, FakeAgentUpdater>(ServiceLifetime.Singleton);
      builder.Services.ReplaceService<IDesktopClientUpdater, FakeDesktopClientUpdater>(ServiceLifetime.Singleton);
      builder.Services.ReplaceService<ICpuUtilizationSampler, FakeCpuUtilizationSampler>(ServiceLifetime.Singleton);
      builder.Services.ReplaceService<ISettingsProvider, FakeSettingsProvider>(ServiceLifetime.Singleton, new FakeSettingsProvider(deviceId, serverUri));
      builder.Services.RemoveImplementation<IpcServerWatcher>();
      builder.Services.RemoveImplementation<AgentHeartbeatTimer>();

      builder.Services.ReplaceService<IDeviceDataGenerator, FakeDeviceDataGenerator>(
        ServiceLifetime.Singleton,
        sp =>
        {
          return new FakeDeviceDataGenerator(
            i,
            tenantId,
            agentVersion);
        });

      var host = builder.Build();
      await host.StartAsync(cancellationToken);
      hosts.Add(host);

      await Delayer.Default.WaitForAsync(
        () =>
        {
          return hosts.All(x => x.Services.GetRequiredService<IHubConnection<IAgentHub>>().IsConnected);
        },
        TimeSpan.FromSeconds(1),
        () =>
        {
          Log.Information("Waiting for all connections to be established.");
          return Task.CompletedTask;
        },
        cancellationToken: cancellationToken);
    });

    var hostTasks = hosts.Select(x => x.WaitForShutdownAsync());
    await Task.WhenAll(hostTasks);

    return;

    static async Task ReportHosts(ConcurrentBag<IHost> hosts, CancellationToken cancellationToken)
    {
      using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
      while (await timer.WaitForNextTickAsync(cancellationToken))
      {
        var hubConnections = hosts.Select(x => { return x.Services.GetRequiredService<IHubConnection<IAgentHub>>(); });

        var groups = hubConnections.GroupBy(x => x.ConnectionState);

        foreach (var group in groups)
        {
          Console.WriteLine($"{group.Key}: {group.Count()}");
        }
      }
    }

    static async Task<Version> GetAgentVersion(Uri serverUri)
    {
      using var client = new HttpClient();
      while (true)
      {
        try
        {
          var version = await client.GetStringAsync(new Uri(serverUri, "/downloads/AgentVersion.txt"));
          return Version.Parse(version.Trim());
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Error getting agent version: {ex.Message}");
          Console.WriteLine("Waiting for backend to be available.");
          await Task.Delay(TimeSpan.FromSeconds(3));
        }
      }
    }
  }
}

