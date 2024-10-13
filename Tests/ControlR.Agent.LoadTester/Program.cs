// See https://aka.ms/new-console-template for more information
using ControlR.Agent.Interfaces;
using ControlR.Agent.LoadTester;
using ControlR.Agent.Models;
using ControlR.Agent.Services.Windows;
using ControlR.Agent.Startup;
using ControlR.Devices.Native.Services;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;

var agentCount = 5;
var connectParallelism = 1;
var serverBase = "https://localhost";
var portStart = 42000;
var portEnd = 44000;
var portCount = portEnd - portStart + 1;

var cancellationTokenSource = new CancellationTokenSource();
var cancellationToken = cancellationTokenSource.Token;

Console.CancelKeyPress += (s, e) =>
{
  cancellationTokenSource.Cancel();
};

var hosts = new ConcurrentBag<IHost>();
var hostTasks = new ConcurrentBag<Task>();
var paralellOptions = new ParallelOptions()
{
  MaxDegreeOfParallelism = connectParallelism,
};

await Parallel.ForAsync(0, agentCount, paralellOptions, (i, ct) =>
{
  if (ct.IsCancellationRequested)
  {
    return ValueTask.CompletedTask;
  }

  var port = portStart + (i % portCount);
  var serverUri = new Uri($"{serverBase}:{port}");

  var appOptions = new AgentAppOptions()
  {
    DeviceId = CreateGuid(i),
    ServerUri = serverUri,
  };

  var builder = Host.CreateApplicationBuilder(args);
  builder.AddControlRAgent(StartupMode.Run, $"loadtester-{i}");
  builder.Configuration
    .GetSection(AgentAppOptions.SectionKey)
    .Bind(appOptions);

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
  var hostTask = host.RunAsync(cancellationToken);
  hosts.Add(host);
  hostTasks.Add(hostTask);

  return ValueTask.CompletedTask;
});

await Task.WhenAll(hostTasks);

return;


static Guid CreateGuid(int seed)
{
  using var md5 = MD5.Create();
  var seedBytes = BitConverter.GetBytes(seed);
  var hash = md5.ComputeHash(seedBytes);
  return new Guid(hash);
}