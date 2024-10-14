// See https://aka.ms/new-console-template for more information
using ControlR.Agent.Interfaces;
using ControlR.Agent.LoadTester;
using ControlR.Agent.Models;
using ControlR.Agent.Services.Windows;
using ControlR.Agent.Startup;
using ControlR.Devices.Native.Services;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;

var agentCount = 1000;
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

await Parallel.ForAsync(0, agentCount, paralellOptions, async (i, ct) =>
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
        { "Logging:LogLevel:Default", "Information" },
        { "Serilog:MinimumLevel:Default", "Information" },
      });

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

  await Task.Delay(100, ct);
});

var hostTasks = hosts.Select(x => x.WaitForShutdownAsync());
await Task.WhenAll(hostTasks);

return;


static Guid CreateGuid(int seed)
{
  using var md5 = MD5.Create();
  var seedBytes = BitConverter.GetBytes(seed);
  var hash = md5.ComputeHash(seedBytes);
  return new Guid(hash);
}