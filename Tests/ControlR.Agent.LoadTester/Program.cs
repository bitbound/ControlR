using System.Collections.Concurrent;
using ControlR.Agent.LoadTester;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Models;
using ControlR.Agent.Common.Services;
using ControlR.Agent.Common.Services.Windows;
using ControlR.Agent.Common.Startup;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Hubs;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Signalr.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

var startCount = 0;

if (args.Length > 0 && int.TryParse(args.Last(), out var lastArg))
{
  startCount = lastArg;
}

Console.WriteLine($"Starting agent count at {startCount}.");

var agentCount = 1;
var serverUri = new Uri("https://localhost:7033");
var tenantId = Guid.Empty;

var cts = new CancellationTokenSource();
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
  builder.AddControlRAgent(StartupMode.Run, $"loadtester-{i}");

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

  builder.Services.Remove(
    builder.Services.First(x => x.ServiceType == typeof(IAgentUpdater)));
  builder.Services.AddSingleton<IAgentUpdater, FakeAgentUpdater>();

  builder.Services.Remove(
    builder.Services.First(x => x.ServiceType == typeof(IStreamerUpdater)));
  builder.Services.AddSingleton<IStreamerUpdater, FakeStreamerUpdater>();

  builder.Services.Remove(
    builder.Services.First(x => x.ServiceType == typeof(ICpuUtilizationSampler)));
  builder.Services.AddSingleton<ICpuUtilizationSampler, FakeCpuUtilizationSampler>();

  builder.Services.Remove(
    builder.Services.First(x => x.ServiceType == typeof(ISettingsProvider)));
  builder.Services.AddSingleton<ISettingsProvider>(new FakeSettingsProvider(deviceId, serverUri));

  builder.Services.Remove(
    builder.Services.First(x => x.ImplementationType == typeof(StreamingSessionWatcher)));

  builder.Services.Remove(
    builder.Services.First(x => x.ImplementationType == typeof(AgentHeartbeatTimer)));

  builder.Services.Remove(
    builder.Services.First(x => x.ServiceType == typeof(IDeviceDataGenerator)));


  builder.Services.AddSingleton<IDeviceDataGenerator>(sp =>
  {
    return new FakeDeviceDataGenerator(
      i,
      tenantId,
      sp.GetRequiredService<ISystemEnvironment>(),
      sp.GetRequiredService<IOptionsMonitor<AgentAppOptions>>(),
      sp.GetRequiredService<ILogger<FakeDeviceDataGenerator>>());
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