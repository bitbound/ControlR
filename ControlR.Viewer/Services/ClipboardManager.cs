namespace ControlR.Viewer.Services;

public interface IClipboardManager
{
  Task SetText(string text);
  Task<string?> GetText();
  Task Start();
}

internal class ClipboardManager(
  IClipboard clipboard,
  IUiThread uiThread,
  IMessenger messenger,
  ILogger<ClipboardManager> logger) : IClipboardManager
{
  private readonly SemaphoreSlim _clipboardLock = new(1, 1);
  private string? _lastClipboardText;


  public async Task<string?> GetText()
  {
    await _clipboardLock.WaitAsync();
    try
    {
      return await uiThread.InvokeAsync(clipboard.GetTextAsync);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting clipboard text.");
    }
    finally
    {
      _clipboardLock.Release();
    }

    return null;
  }

  public async Task SetText(string? text)
  {
    await _clipboardLock.WaitAsync();
    try
    {
      await uiThread.InvokeAsync(async () =>
      {
        _lastClipboardText = text;
        await clipboard.SetTextAsync(text);
      });
    }
    catch (OperationCanceledException)
    {
      logger.LogInformation("Clipboard manager disposed.  Aborting watch.");
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while watching the clipboard.");
    }
    finally
    {
      _clipboardLock.Release();
    }
  }

  public async Task Start()
  {
    await uiThread.InvokeAsync(async () =>
    {
      await TrySetInitialText();
      clipboard.ClipboardContentChanged -= HandleClipboardContentChange;
      clipboard.ClipboardContentChanged += HandleClipboardContentChange;
    });
  }

  private async void HandleClipboardContentChange(object? sender, EventArgs e)
  {
    await _clipboardLock.WaitAsync();
    try
    {
      var clipboardText = await clipboard.GetTextAsync();
      if (clipboardText is null || clipboardText == _lastClipboardText)
      {
        return;
      }

      _lastClipboardText = clipboardText;
      await messenger.Send(new LocalClipboardChangedMessage(clipboardText));
    }
    catch (OperationCanceledException)
    {
      logger.LogInformation("Clipboard manager disposed.  Aborting watch.");
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while watching the clipboard.");
    }
    finally
    {
      _clipboardLock.Release();
    }
  }

  private async Task TrySetInitialText()
  {
    try
    {
      await _clipboardLock.WaitAsync();
      await uiThread.InvokeAsync(async () => { _lastClipboardText = await clipboard.GetTextAsync(); });
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Failed to get initial clipboard text.");
    }
    finally
    {
      _clipboardLock.Release();
    }
  }
}