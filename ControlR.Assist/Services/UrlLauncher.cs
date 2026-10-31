using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Logging;

namespace ControlR.Assist.Services;

internal sealed class UrlLauncher(IProcessManager processManager, ILogger<UrlLauncher> logger) : IUrlLauncher
{
  private readonly ILogger<UrlLauncher> _logger = logger;
  private readonly IProcessManager _processManager = processManager;

  public bool Open(string target)
  {
    if (string.IsNullOrWhiteSpace(target))
    {
      return false;
    }

    try
    {
      return _processManager.LaunchUri(new Uri(target)) is not null;
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to open target {Target}.", target);
      return false;
    }
  }
}