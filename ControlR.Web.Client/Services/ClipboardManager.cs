namespace ControlR.Web.Client.Services;

public interface IClipboardManager
{
  Task SetText(string text);
  Task<string?> GetText();
}

internal class ClipboardManager: IClipboardManager
{
  public async Task<string?> GetText()
  {
    await Task.Yield();
    return "";
  }

  public async Task SetText(string? text)
  {
    await Task.Yield();
  }
}