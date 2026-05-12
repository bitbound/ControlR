using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Linux.XdgPortal;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Linux.Services;

public class ClipboardManagerWayland(
  IXdgDesktopPortal desktopPortal,
  ILogger<ClipboardManagerWayland> logger) : IClipboardManager
{
  private readonly IXdgDesktopPortal _desktopPortal = desktopPortal;
  private readonly ILogger<ClipboardManagerWayland> _logger = logger;

  public async Task<string?> GetText()
  {
    try
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      return await _desktopPortal.GetClipboardText(cts.Token).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting clipboard text via XDG portal");
      return null;
    }
  }

  public async Task SetText(string? text)
  {
    try
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await _desktopPortal.SetClipboardText(text ?? string.Empty, cts.Token).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error setting clipboard text via XDG portal");
    }
  }
}
