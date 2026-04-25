using System.Diagnostics;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Services;

internal sealed class UrlLauncher(ILogger<UrlLauncher> logger) : IUrlLauncher
{
  private readonly ILogger<UrlLauncher> _logger = logger;

  public bool Open(string target)
  {
    if (string.IsNullOrWhiteSpace(target))
    {
      return false;
    }

    try
    {
      Process.Start(new ProcessStartInfo
      {
        FileName = target,
        UseShellExecute = true
      });

      return true;
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to open target {Target}.", target);
      return false;
    }
  }
}