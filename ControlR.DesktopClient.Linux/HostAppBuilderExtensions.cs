using Microsoft.Extensions.Hosting;
using ControlR.Libraries.DevicesCommon.Extensions;
using ControlR.Libraries.NativeInterop.Unix;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.DesktopClient.Linux;

public static class HostAppBuilderExtensions
{
  public static IHostApplicationBuilder AddLinuxDesktopServices(
    this IHostApplicationBuilder builder,
    string appDataFolder)
  {
    builder.Services
      .AddSingleton<IFileSystemUnix, FileSystemUnix>();
      
    builder.BootstrapSerilog(
      logFilePath: PathConstants.GetLogsPath(appDataFolder),
      logRetention: TimeSpan.FromDays(7));

    return builder;
  }
}
