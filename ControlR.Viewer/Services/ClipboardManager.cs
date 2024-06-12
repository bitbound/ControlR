using Microsoft.Extensions.Logging;

namespace ControlR.Viewer.Services;

public interface IClipboardManager
{
    Task SetText(string text);
    Task<string?> GetText();

    Task Start();
}

internal class ClipboardManager(
    IClipboard _clipboard,
    IUiThread _uiThread,
    IMessenger _messenger,
    ILogger<ClipboardManager> _logger) : IClipboardManager
{
    private readonly SemaphoreSlim _clipboardLock = new(1, 1);
    private string? _lastClipboardText;


    public async Task<string?> GetText()
    {
        await _clipboardLock.WaitAsync();
        try
        {
            return await _uiThread.InvokeAsync(_clipboard.GetTextAsync);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting clipboard text.");
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
            await _uiThread.InvokeAsync(async () =>
            {
                _lastClipboardText = text;
                await _clipboard.SetTextAsync(text);
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Clipboard manager disposed.  Aborting watch.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while watching the clipboard.");
        }
        finally
        {
            _clipboardLock.Release();
        }
    }

    public async Task Start()
    {
        await _uiThread.InvokeAsync(async () =>
        {
            await TrySetInitialText();
            _clipboard.ClipboardContentChanged -= HandleClipboardContentChange;
            _clipboard.ClipboardContentChanged += HandleClipboardContentChange;

        });
    }

    private async void HandleClipboardContentChange(object? sender, EventArgs e)
    {
        await _clipboardLock.WaitAsync();
        try
        {
            var clipboardText = await _clipboard.GetTextAsync();
            if (clipboardText is null || clipboardText == _lastClipboardText)
            {
                return;
            }

            _lastClipboardText = clipboardText;
            await _messenger.Send(new LocalClipboardChangedMessage(clipboardText));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Clipboard manager disposed.  Aborting watch.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while watching the clipboard.");
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
            await _uiThread.InvokeAsync(async () =>
            {
                _lastClipboardText = await _clipboard.GetTextAsync();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get initial clipboard text.");
        }
        finally
        {
            _clipboardLock.Release();
        }
    }
}
