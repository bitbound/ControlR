using ControlR.Shared.Dtos;
using ControlR.Shared.Primitives;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Interfaces;
internal interface IStreamerUpdater : IHostedService
{
    Task<Result> EnsureLatestVersion(
        StreamerSessionRequestDto requestDto,
        CancellationToken cancellationToken);
}
