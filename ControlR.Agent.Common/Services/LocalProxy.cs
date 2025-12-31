using ControlR.Libraries.Shared.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Services.Buffers;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Sockets;

namespace ControlR.Agent.Common.Services;

internal interface ILocalSocketProxy
{
  Task<Result> HandleVncSession(VncSessionRequestDto vncSessionRequestDto);
  Task<Result> TestConnection(int port, CancellationToken cancellationToken);
}

internal class LocalSocketProxy(
    IHostApplicationLifetime appLifetime,
    IMemoryProvider memoryProvider,
    ILogger<LocalSocketProxy> logger) : TcpWebsocketProxyBase(memoryProvider, logger), ILocalSocketProxy
{
  public async Task<Result> HandleVncSession(VncSessionRequestDto vncSessionRequestDto)
  {

    return await ProxyToLocalService(
        vncSessionRequestDto.SessionId,
        vncSessionRequestDto.Port,
        vncSessionRequestDto.WebsocketUri,
        appLifetime.ApplicationStopping);
  }

  public async Task<Result> TestConnection(int port, CancellationToken cancellationToken)
  {
    try
    {
      using var tcpClient = new TcpClient();
      await tcpClient.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
      return Result.Ok();
    }
    catch (OperationCanceledException ex)
    {
      _logger.LogWarning(ex, "VNC connection test canceled on port {Port}", port);
      return Result.Fail("VNC connection test timed out or was cancelled on the remote device.");
    }
    catch (SocketException ex)
    {
      _logger.LogError(ex, "Error while testing VNC connection on port {Port}.", port);
      if (ex.SocketErrorCode == SocketError.ConnectionRefused)
      {
        return Result.Fail("The VNC service is not running or not reachable on the remote device.");
      }
      return Result.Fail($"A socket exception occurred on the remote device with error code '{ex.SocketErrorCode}'.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while testing VNC connection on port {Port}", port);
      return Result.Fail("An error occurred while testing VNC connection on the remote device.");
    }
  }
}