using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.Hosting;

namespace ControlR.DesktopClient.Models;

public sealed class RemoteControlSession(
  RemoteControlRequestIpcDto requestDto,
  IHost host) : IAsyncDisposable
{
  public CancellationTokenSource CancellationTokenSource { get; } = new();
  public IHost Host { get; } = host;
  public RemoteControlRequestIpcDto RequestDto { get; } = requestDto;

  public async ValueTask DisposeAsync()
  {
    try
    {
      Disposer.DisposeAll(CancellationTokenSource, Host);
    }
    catch
    {
      // Ignore.
    }
  }
}