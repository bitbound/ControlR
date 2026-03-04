using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.Hosting;

namespace ControlR.DesktopClient.Models;

public sealed class RemoteControlSession(
  RemoteControlRequestIpcDto requestDto,
  IHost host,
  DateTimeOffset connectedAt) : IAsyncDisposable
{
  public DateTimeOffset ConnectedAt { get; } = connectedAt;
  public IHost Host { get; } = host;
  public RemoteControlRequestIpcDto RequestDto { get; } = requestDto;

  public Guid SessionId => RequestDto.SessionId;

  public async ValueTask DisposeAsync()
  {
    try
    {
      await Host.StopAsync();
    }
    catch
    {
      // Ignore.
    }
    finally
    {
      Disposer.DisposeAll(Host);
    }
  }
}