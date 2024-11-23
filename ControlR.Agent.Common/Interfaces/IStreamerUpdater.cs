using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Interfaces;
internal interface IStreamerUpdater : IHostedService
{
  Task<bool> EnsureLatestVersion(
      StreamerSessionRequestDto requestDto,
      CancellationToken cancellationToken);

  Task<bool> EnsureLatestVersion(
      CancellationToken cancellationToken);
}
