using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Libraries.Shared.Extensions;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Windows.Services;

internal class ClipboardManagerWindows(
  IWin32Interop win32Interop,
  ILogger<ClipboardManagerWindows> logger) : IClipboardManager
{
  private readonly SemaphoreSlim _clipboardLock = new(1, 1);
  private readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(5);
  private readonly ILogger<ClipboardManagerWindows> _logger = logger;
  private readonly IWin32Interop _win32Interop = win32Interop;

  public async Task<string?> GetText()
  {
    using var cts = new CancellationTokenSource(_lockTimeout);
    using var acquiredLock = await _clipboardLock.AcquireLockAsync(cts.Token);
    try
    {
      return _win32Interop.GetClipboardText();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting clipboard text.");
      return null;
    }
  }

  public async Task SetText(string? text)
  {
    using var cts = new CancellationTokenSource(_lockTimeout);
    using var acquiredLock = await _clipboardLock.AcquireLockAsync(cts.Token);
    try
    {
      _win32Interop.SetClipboardText(text);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while setting clipboard text.");
    }
  }
}