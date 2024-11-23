using ControlR.Agent.Common.Services;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.LoadTester;
internal class FakeAgentUpdater : IHostedService, IAgentUpdater
{
  public ManualResetEventAsync UpdateCheckCompletedSignal { get; } = new();

  public Task CheckForUpdate(CancellationToken cancellationToken = default)
  {
    UpdateCheckCompletedSignal.Set();
    return Task.CompletedTask;
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
