using ControlR.Shared.Dtos;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Interfaces;
internal interface IStreamerUpdater : IHostedService
{
    Task<bool> EnsureLatestVersion(
        StreamerSessionRequestDto requestDto,
        CancellationToken cancellationToken);
}
