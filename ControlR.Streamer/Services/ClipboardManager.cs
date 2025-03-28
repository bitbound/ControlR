using Windows.Win32;

namespace ControlR.Streamer.Services;

public interface IClipboardManager
{
  Task<string?> GetText();

  Task SetText(string? text);
}

internal partial class ClipboardManager(
  IWin32Interop win32Interop,
  ILogger<ClipboardManager> logger) : IClipboardManager
{
  private readonly SemaphoreSlim _clipboardLock = new(1, 1);
  private readonly IWin32Interop _win32Interop = win32Interop;
  private readonly ILogger<ClipboardManager> _logger = logger;

  public async Task<string?> GetText()
  {
    await _clipboardLock.WaitAsync();
    try
    {
      return _win32Interop.GetClipboardText();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting clipboard text.");
      return null;
    }
    finally
    {
      _clipboardLock.Release();
    }
  }

  public async Task SetText(string? text)
  {
    await _clipboardLock.WaitAsync();
    try
    {
      _win32Interop.SetClipboardText(text);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while setting clipboard text.");
    }
    finally
    {
      PInvoke.CloseClipboard();
      _clipboardLock.Release();
    }
  }
}