using Bitbound.SimpleMessenger;
using ControlR.Viewer.Models.Messages;
using Microsoft.Extensions.Hosting;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace ControlR.Streamer.Services;
public interface IClipboardManager : IHostedService
{
    Task<string?> GetText();

    Task SetText(string? text);
}
internal class ClipboardManager(
    IMessenger _messenger,
    IWin32Interop _win32Interop,
    IDelayer _delayer,
    ILogger<ClipboardManager> _logger) : BackgroundService, IClipboardManager
{
    private readonly SemaphoreSlim _clipboardLock = new(1, 1);
    private string? _clipboardText;

    public async Task<string?> GetText()
    {
        await _clipboardLock.WaitAsync();
        try
        {
            _logger.LogError("Open clipboard failed when trying to get text.");
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
            _clipboardText = text;
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _clipboardText = _win32Interop.GetClipboardText();

       while (!stoppingToken.IsCancellationRequested)
        {
            await _clipboardLock.WaitAsync(stoppingToken);
            try
            {
                var clipboardText = _win32Interop.GetClipboardText();
                if (clipboardText is not null && clipboardText != _clipboardText)
                {
                    _logger.LogDebug("Clipboard text changed.");
                    _clipboardText = clipboardText;
                    _messenger.Send(new LocalClipboardChangedMessage(clipboardText)).Forget();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting clipboard data.");
            }
            finally
            {
                _clipboardLock.Release();
            }

            await _delayer.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
        }
    }


}
