using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace ControlR.Libraries.DevicesCommon.Services;
public class HostLifetimeEventResponder(
    IHostApplicationLifetime _appLifetime,
    ILogger<HostLifetimeEventResponder> _logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _appLifetime.ApplicationStarted.Register(() =>
        {
            var exeVersion = Assembly.GetExecutingAssembly().GetName().Version;
            _logger.LogInformation("Host initialized.  Assembly version: {AsmVersion}.", exeVersion);
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
