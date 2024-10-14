﻿using ControlR.Agent.LoadTester;
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
var agentCount = 4000;
var connectParallelism = 100;
var serverBase = "http://cubey";
var portStart = 42000;
var portEnd = 42999;
var portCount = portEnd - portStart + 1;

var cancellationTokenSource = new CancellationTokenSource();
var cancellationToken = cancellationTokenSource.Token;

Console.CancelKeyPress += (s, e) =>
{
  cancellationTokenSource.Cancel();
};

var hosts = new ConcurrentBag<IHost>();
var paralellOptions = new ParallelOptions()
{
  MaxDegreeOfParallelism = connectParallelism
};

_ = ReportHosts(hosts, cancellationToken);

await Parallel.ForAsync(startCount, startCount + agentCount, paralellOptions, async (i, ct) =>
{
  if (ct.IsCancellationRequested)
  {
    return;
  }

  var port = portStart + (i % portCount);
  var serverUri = $"{serverBase}:{port}";

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

  var cpuSampler = builder.Services.First(x => x.ServiceType == typeof(ICpuUtilizationSampler));
  builder.Services.Remove(cpuSampler);
  builder.Services.AddSingleton<ICpuUtilizationSampler, FakeCpuUtilizationSampler>();

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

  await Task.Delay(1000, ct);
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