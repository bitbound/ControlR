using ControlR.Libraries.Shared.Dtos.RemoteControlDtos;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Interfaces;

internal interface IDesktopClientUpdater : IHostedService
{
  Task<bool> EnsureLatestVersion(
    bool acquireGlobalLock,
    CancellationToken cancellationToken);
}