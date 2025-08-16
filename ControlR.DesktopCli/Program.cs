using System.CommandLine;
using ControlR.DesktopCli;
using ControlR.DesktopCli.Services;
using ControlR.DesktopClient.Common.Options;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.Shared.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
#if MAC_BUILD
using ControlR.Libraries.NativeInterop.Unix.MacOs;
using ControlR.DesktopClient.Mac.Services;
#endif

var instanceIdOption = new Option<string?>(
  "InstanceId",
  ["-i", "--instance-id"])
{
  Description =
    "An instance ID for this agent installation, which differentiates multiple agent installations.  " +
    "This is typically the server origin (e.g. 'example.controlr.app')."
};

var rootCommand = new RootCommand("Open-source remote control client.")
{
  instanceIdOption
};

rootCommand.SetAction(async parseResult =>
{
  var instanceId = parseResult.GetValue(instanceIdOption);

  var builder = Host.CreateApplicationBuilder(args);
  var services = builder.Services;

  builder.AddSerilog(instanceId);

  var configData = new Dictionary<string, string?>();
  if (!string.IsNullOrEmpty(instanceId))
  {
    configData["InstanceOptions:InstanceId"] = instanceId.SanitizeForFileSystem();
  }

  builder.Configuration.AddInMemoryCollection(configData);

  services.Configure<DesktopClientOptions>(options =>
  {
    options.InstanceId = instanceId;
  });

  services.AddControlrIpc();
  services.AddSingleton(TimeProvider.System);
  services.AddSingleton<IProcessManager, ProcessManager>();
  services.AddHostedService<IpcClientManager>();

  #if MAC_BUILD
    if (OperatingSystem.IsMacOS())
    {
      services.AddHostedService<PermissionsInitializerMac>();
      services.AddSingleton<IMacInterop, MacInterop>();
    }
#endif

  await builder.Build().RunAsync();
});

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();