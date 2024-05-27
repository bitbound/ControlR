using Microsoft.Extensions.Logging;

namespace ControlR.Viewer.Services;

public interface IClipboardManager : IAsyncDisposable
{
    event EventHandler<string?>? ClipboardChanged;
    Task SetText(string text);

    Task Start();
}

internal class ClipboardManager(
    IClipboard _clipboard,
    ILogger<ClipboardManager> _logger) : IClipboardManager
{
    // This service is transient in DI, but we want all instances to share
    // a lock for accessing the clipboard.
    private static readonly SemaphoreSlim _clipboardLock = new(1, 1);
    private readonly CancellationTokenSource _cancellationSource = new();
    private string? _lastClipboardText;

    public event EventHandler<string?>? ClipboardChanged;
    public async ValueTask DisposeAsync()
    {
        try
        {
            await _cancellationSource.CancelAsync();
            _cancellationSource.Dispose();
        }
        catch { }
    }

    public async Task SetText(string? text)
    {
        var cancellationToken = _cancellationSource.Token;

        await _clipboardLock.WaitAsync(cancellationToken);
        try
        {
            _lastClipboardText = text;
            await _clipboard.SetTextAsync(text);
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
        await TrySetInitialText(_cancellationSource.Token);
        _clipboard.ClipboardContentChanged -= HandleClipboardContentChange;
        _clipboard.ClipboardContentChanged += HandleClipboardContentChange;
    }

    private async void HandleClipboardContentChange(object? sender, EventArgs e)
    {
        await _clipboardLock.WaitAsync(_cancellationSource.Token);
        try
        {
            var clipboardText = await _clipboard.GetTextAsync();
            if (clipboardText is null || clipboardText == _lastClipboardText)
            {
                return;
            }

            _lastClipboardText = clipboardText;
            ClipboardChanged?.Invoke(this, clipboardText);
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

    private async Task TrySetInitialText(CancellationToken cancellationToken)
    {
        try
        {
            await _clipboardLock.WaitAsync();
            _lastClipboardText = await _clipboard.GetTextAsync();
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
