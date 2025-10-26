using Avalonia.Input.Platform;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Services;

public class ClipboardManagerAvalonia(
  ILogger<ClipboardManagerAvalonia> logger) : IClipboardManager
{
  private readonly ILogger<ClipboardManagerAvalonia> _logger = logger;
  public async Task<string?> GetText()
  {
    return await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
    { 
      if (App.MainWindow.Clipboard is null)
      {
        _logger.LogWarning("Clipboard is not available.");
        return null;
      }

      return await App.MainWindow.Clipboard.TryGetTextAsync();
    });
  }

  public async Task SetText(string? text)
  {
    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
    {
      if (App.MainWindow.Clipboard is null)
      {
        _logger.LogWarning("Clipboard is not available.");
        return;
      }
      await App.MainWindow.Clipboard.SetTextAsync(text);
    });
  }
}
