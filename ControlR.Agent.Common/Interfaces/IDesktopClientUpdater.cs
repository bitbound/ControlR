using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Interfaces;
internal interface IDesktopClientUpdater : IHostedService
{
  Task<bool> EnsureLatestVersion(
      RemoteControlSessionRequestDto requestDto,
      CancellationToken cancellationToken);

  Task<bool> EnsureLatestVersion(
      CancellationToken cancellationToken);
}
