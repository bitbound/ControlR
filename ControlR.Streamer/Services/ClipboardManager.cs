using Windows.Win32;
using Bitbound.SimpleMessenger;
using Microsoft.Extensions.Hosting;

namespace ControlR.Streamer.Services;

public interface IClipboardManager : IHostedService
{
  Task<string?> GetText();

  Task SetText(string? text);
}

internal class ClipboardManager(
  IMessenger messenger,
  IWin32Interop win32Interop,
  IDelayer delayer,
  ILogger<ClipboardManager> logger) : BackgroundService, IClipboardManager
{
  private readonly SemaphoreSlim _clipboardLock = new(1, 1);
  private string? _clipboardText;

  public async Task<string?> GetText()
  {
    await _clipboardLock.WaitAsync();
    try
    {
      logger.LogError("Open clipboard failed when trying to get text.");
      return win32Interop.GetClipboardText();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting clipboard text.");
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
      _clipboardText = text;
      win32Interop.SetClipboardText(text);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while setting clipboard text.");
    }
    finally
    {
      PInvoke.CloseClipboard();
      _clipboardLock.Release();
    }
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    _clipboardText = win32Interop.GetClipboardText();

    while (!stoppingToken.IsCancellationRequested)
    {
      await _clipboardLock.WaitAsync(stoppingToken);
      try
      {
        var clipboardText = win32Interop.GetClipboardText();
        if (clipboardText is not null && clipboardText != _clipboardText)
        {
          logger.LogDebug("Clipboard text changed.");
          _clipboardText = clipboardText;
          messenger.Send(new LocalClipboardChangedMessage(clipboardText)).Forget();
        }
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error while getting clipboard data.");
      }
      finally
      {
        _clipboardLock.Release();
      }

      await delayer.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
    }
  }
}