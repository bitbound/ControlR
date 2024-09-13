namespace ControlR.Web.Client.Services;

public interface IClipboardManager
{
    Task SetText(string text);
    Task<string?> GetText();
}

internal class ClipboardManager(
    IMessenger _messenger,
    ILogger<ClipboardManager> _logger) : IClipboardManager
{
    private readonly SemaphoreSlim _clipboardLock = new(1, 1);
    private string? _lastClipboardText;


    public async Task<string?> GetText()
    {
        return "";
    }

    public async Task SetText(string? text)
    {
        return;
    }
}
