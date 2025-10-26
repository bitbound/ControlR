namespace ControlR.Web.Client.Services;

public interface IClipboardManager
{
  Task<string?> GetText();
  Task SetText(string text);
}

internal class ClipboardManager(IJsInterop jsInterop) : IClipboardManager
{
  private readonly IJsInterop _jsInterop = jsInterop;

  public async Task<string?> GetText()
  {
    return await _jsInterop.GetClipboardText();
  }

  public async Task SetText(string? text)
  {
    await _jsInterop.SetClipboardText(text);
  }
}