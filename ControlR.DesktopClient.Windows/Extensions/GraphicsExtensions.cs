using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;
using System.Drawing;
using Windows.Win32.Foundation;

namespace ControlR.DesktopClient.Windows.Extensions;

internal static class GraphicsExtensions
{
  public static DisposableValue<nint> GetDisposableHdc(this Graphics graphics, ILogger? logger = null)
  {
    return new DisposableValue<nint>(
      value: graphics.GetHdc(),
      disposeCallback: graphics.ReleaseHdc,
      logger: logger);
  }
  public static Rectangle ToRectangle(this RECT rect)
  {
    return new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
  }
}