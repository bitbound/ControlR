using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using Microsoft.Extensions.Hosting;

namespace ControlR.Libraries.Agent.Interfaces;
internal interface IStreamerUpdater : IHostedService
{
  Task<bool> EnsureLatestVersion(
      StreamerSessionRequestDto requestDto,
      CancellationToken cancellationToken);

  Task<bool> EnsureLatestVersion(
      CancellationToken cancellationToken);
}
