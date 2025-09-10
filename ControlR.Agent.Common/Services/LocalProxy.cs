using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Services.Buffers;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services;

internal interface ILocalSocketProxy
{
  Task<Result> HandleVncSession(VncSessionRequestDto vncSessionRequestDto);

}

internal class LocalSocketProxy(
    IHostApplicationLifetime appLifetime,
    ISettingsProvider settings,
    IMemoryProvider memoryProvider,
    IRetryer retryer,
    ILogger<LocalSocketProxy> logger) : TcpWebsocketProxyBase(memoryProvider, retryer, logger), ILocalSocketProxy
{
  public async Task<Result> HandleVncSession(VncSessionRequestDto vncSessionRequestDto)
  {

    return await ProxyToLocalService(
        vncSessionRequestDto.SessionId,
        settings.VncPort,
        vncSessionRequestDto.WebsocketUri,
        appLifetime.ApplicationStopping);
  }

}