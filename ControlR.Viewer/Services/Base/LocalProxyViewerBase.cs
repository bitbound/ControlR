using Microsoft.Extensions.Logging;

namespace ControlR.Viewer.Services.Base;

internal class LocalProxyViewerBase(
    ISettings _settings,
    ILogger<LocalProxyViewerBase> _logger)
{
    private readonly SemaphoreSlim _proxyLock = new(1, 1);

    protected async Task<Result> ProxyConnections()
    {
        if (!await _proxyLock.WaitAsync(0))
        {
            return Result.Fail("Local proxy is already in use.");
        }

        _logger.LogInformation("Starting local proxy service.");
        return Result.Ok();
    }
}