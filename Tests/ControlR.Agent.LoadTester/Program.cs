using ControlR.Agent.LoadTester;
using ControlR.Devices.Native.Services;
using ControlR.Libraries.Agent.Interfaces;
using ControlR.Libraries.Agent.Models;
using ControlR.Libraries.Agent.Services;
using ControlR.Libraries.Agent.Services.Windows;
using ControlR.Libraries.Agent.Startup;
using ControlR.Libraries.Shared.Hubs;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Signalr.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Collections.Concurrent;
using System.Security.Cryptography;

var startCount = 0;

if (args.Length > 0 && int.TryParse(args.Last(), out var lastArg))
{
  startCount = lastArg;
}

Console.WriteLine($"Starting agent count at {startCount}.");

var agentCount = 4000;
var serverUri = "http://192.168.0.2:5003/";


var cts = new CancellationTokenSource();
var cancellationToken = cts.Token;

Console.CancelKeyPress += (s, e) =>
{
  cts.Cancel();
};

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

  builder.Configuration
    .AddInMemoryCollection(new Dictionary<string, string?>
      {
        { "AppOptions:DeviceId", CreateGuid(i).ToString() },
        { "AppOptions:ServerUri", serverUri },
        { "Logging:LogLevel:Default", "Warning" },
        { "Serilog:MinimumLevel:Default", "Warning" },
      });

  var agentUpdater = builder.Services.First(x => x.ServiceType == typeof(IAgentUpdater));
  builder.Services.Remove(agentUpdater);
  builder.Services.AddSingleton<IAgentUpdater, FakeAgentUpdater>();

  var streamerUpdater = builder.Services.First(x => x.ServiceType == typeof(IStreamerUpdater));
  builder.Services.Remove(streamerUpdater);
  builder.Services.AddSingleton<IStreamerUpdater, FakeStreamerUpdater>();

  var cpuSampler = builder.Services.First(x => x.ServiceType == typeof(ICpuUtilizationSampler));
  builder.Services.Remove(cpuSampler);
  builder.Services.AddSingleton<ICpuUtilizationSampler, FakeCpuUtilizationSampler>();

  var sessionWatcher = builder.Services.First(x => x.ImplementationType == typeof(StreamingSessionWatcher));
  builder.Services.Remove(sessionWatcher);

  var deviceDataGenerator = builder.Services.First(x => x.ServiceType == typeof(IDeviceDataGenerator));
  builder.Services.Remove(deviceDataGenerator);

  builder.Services.AddSingleton<IDeviceDataGenerator>(sp =>
  {
    return new FakeDeviceDataGenerator(
      i, 
      sp.GetRequiredService<IWin32Interop>(), 
      sp.GetRequiredService<ISystemEnvironment>(), 
      sp.GetRequiredService<ILogger<DeviceDataGeneratorWin>>());
  });

  var host = builder.Build();
  await host.StartAsync(cancellationToken);
  hosts.Add(host);

  await Delayer.Default.WaitForAsync(
    condition: () =>
    {
      return hosts.All(x =>
      {
        return x.Services.GetRequiredService<IHubConnection<IAgentHub>>().IsConnected;
      });
    },
    pollingDelay: TimeSpan.FromSeconds(1),
    conditionFailedCallback: () =>
    {
      Log.Information("Waiting for all connections to be established.");
      return Task.CompletedTask;
    },
    cancellationToken: cancellationToken);
});

var hostTasks = hosts.Select(x => x.WaitForShutdownAsync());
await Task.WhenAll(hostTasks);

return;


static Guid CreateGuid(int seed)
{
  var seedBytes = BitConverter.GetBytes(seed);
  var hash = MD5.HashData(seedBytes);
  return new Guid(hash);
}

static async Task ReportHosts(ConcurrentBag<IHost> hosts, CancellationToken cancellationToken)
{
  using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
  while (await timer.WaitForNextTickAsync(cancellationToken))
  {
    var hubConnections = hosts.Select(x =>
    {
      return x.Services.GetRequiredService<IHubConnection<IAgentHub>>();
    });

    var groups = hubConnections.GroupBy(x => x.ConnectionState);

    foreach (var group in groups)
    {
      Console.WriteLine($"{group.Key}: {group.Count()}");
    }
  }
}