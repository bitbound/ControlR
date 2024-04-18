using ControlR.Agent.Interfaces;
using ControlR.Shared.Dtos;
using ControlR.Shared.Extensions;
using ControlR.Shared.Primitives;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Services.Fakes;
internal class StreamerUpdaterFake : BackgroundService, IStreamerUpdater
{
    public Task<Result> EnsureLatestVersion(StreamerSessionRequestDto requestDto, CancellationToken cancellationToken)
    {
        return Result.Fail("Platform not supported.").AsTaskResult();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}
