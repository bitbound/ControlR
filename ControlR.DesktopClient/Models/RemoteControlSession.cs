using ControlR.Libraries.Shared.Dtos.IpcDtos;
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
      Disposer.DisposeAll(Host);
    }
    catch
    {
      // Ignore.
    }
  }
}