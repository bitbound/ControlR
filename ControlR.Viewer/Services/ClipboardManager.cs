using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ControlR.Viewer.Services;

public interface IClipboardManager : IAsyncDisposable
{
    event EventHandler<string?>? ClipboardChanged;
    Task SetText(string text);

    void Start();
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

    private Task? _watcherTask;

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

    public void Start()
    {
        if (_watcherTask is not null)
        {
            return;
        }

        _watcherTask = StartWatching(_cancellationSource.Token);
    }
    private async Task StartWatching(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await _clipboardLock.WaitAsync(cancellationToken);
            try
            {
                var clipboardText = await _clipboard.GetTextAsync();
                if (clipboardText == _lastClipboardText)
                {
                    continue;
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
    }
}
