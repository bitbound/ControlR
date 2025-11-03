using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.LoadTester;
// ReSharper disable once ClassNeverInstantiated.Global
internal class FakeDesktopClientUpdater : IHostedService, IDesktopClientUpdater
{
  public Task<bool> EnsureLatestVersion(bool acquireGlobalLock, CancellationToken cancellationToken)
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
