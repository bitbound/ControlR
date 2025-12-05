using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.DevicesCommon.Services;

public class HostLifetimeEventResponder(
  IHostApplicationLifetime appLifetime,
  ILogger<HostLifetimeEventResponder> logger) : IHostedService
{
  public Task StartAsync(CancellationToken cancellationToken)
  {
    appLifetime.ApplicationStarted.Register(() =>
    {
      var exeVersion = Assembly.GetExecutingAssembly().GetName().Version;
      logger.LogInformation("Host initialized.  Assembly version: {AsmVersion}.", exeVersion);
    });
    appLifetime.ApplicationStopping.Register(() =>
    {
      logger.LogInformation("Host is stopping.");
    });
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
  }
}