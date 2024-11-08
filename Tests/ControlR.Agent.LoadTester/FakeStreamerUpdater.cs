using ControlR.Libraries.Agent.Interfaces;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.LoadTester;
internal class FakeStreamerUpdater : IHostedService, IStreamerUpdater
{

  public Task<bool> EnsureLatestVersion(StreamerSessionRequestDto requestDto, CancellationToken cancellationToken)
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
