using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.LoadTester;
internal class FakeDesktopClientUpdater : IHostedService, IDesktopClientUpdater
{
  public Task<bool> EnsureLatestVersion(RemoteControlSessionRequestDto requestDto, CancellationToken cancellationToken)
  {
    return Task.FromResult(true);
  }

  public Task<bool> EnsureLatestVersion(CancellationToken cancellationToken)
  {
    return Task.FromResult(true);
  }

  public Task StartAsync(CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
  }
}
