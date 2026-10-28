using ControlR.Libraries.Shared.Primitives;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;

public interface IClipboardManager
{
  Task<string?> GetText();

  Task<Result> SetText(string? text);
}
