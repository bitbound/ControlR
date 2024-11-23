using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services.Fakes;

internal class StreamerUpdaterFake(ILogger<StreamerUpdaterFake> logger) : BackgroundService, IStreamerUpdater
{
  public Task<bool> EnsureLatestVersion(StreamerSessionRequestDto requestDto, CancellationToken cancellationToken)
  {
    logger.LogWarning("Platform not supported for desktop streaming.");
    return Task.FromResult(false);
  }

  public Task<bool> EnsureLatestVersion(CancellationToken cancellationToken)
  {
    return Task.FromResult(true);
  }

  protected override Task ExecuteAsync(CancellationToken stoppingToken)
  {
    return Task.CompletedTask;
  }
}