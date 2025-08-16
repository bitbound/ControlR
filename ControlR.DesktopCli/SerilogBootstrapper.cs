using Microsoft.Extensions.Hosting;
using ControlR.Libraries.DevicesCommon.Extensions;
using ControlR.Libraries.Shared.Services;

namespace ControlR.DesktopCli;

internal static class SerilogBootstrapper
{
  public static IHostApplicationBuilder AddSerilog(
    this IHostApplicationBuilder builder,
    string? instanceId)
  {
    var logsPath = string.Empty;

#if WINDOWS_BUILD
    logsPath = DesktopClient.Windows.PathConstants.GetLogsPath(instanceId);
#elif MAC_BUILD
    logsPath = DesktopClient.Mac.PathConstants.GetLogsPath(instanceId);
#elif LINUX_BUILD
    logsPath = DesktopClient.Linux.PathConstants.GetLogsPath(instanceId);
#else
    throw new PlatformNotSupportedException("Unsupported operating system.");
#endif
    builder.Services.BootstrapSerilog(
      builder.Configuration,
      logsPath,
      TimeSpan.FromDays(7),
      config =>
      {
        if (SystemEnvironment.Instance.IsDebug)
        {
          config.MinimumLevel.Debug();
        }
      });
    return builder;
  }
}
