using System.Drawing;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Libraries.ScreenCapture.Extensions;

public static class GraphicsExtensions
{
  public static DisposableValue<nint> GetDisposableHdc(this Graphics graphics)
  {
    return new DisposableValue<nint>(
      value: graphics.GetHdc(),
      disposeCallback: graphics.ReleaseHdc);
  } 
}