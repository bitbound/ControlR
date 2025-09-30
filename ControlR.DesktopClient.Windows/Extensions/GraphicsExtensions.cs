using ControlR.Libraries.Shared.Primitives;
using System.Drawing;
using Windows.Win32.Foundation;

namespace ControlR.DesktopClient.Windows.Extensions;

internal static class GraphicsExtensions
{
  public static DisposableValue<nint> GetDisposableHdc(this Graphics graphics)
  {
    return new DisposableValue<nint>(
      value: graphics.GetHdc(),
      disposeCallback: graphics.ReleaseHdc);
  }
  public static Rectangle ToRectangle(this RECT rect)
  {
    return new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
  }
}