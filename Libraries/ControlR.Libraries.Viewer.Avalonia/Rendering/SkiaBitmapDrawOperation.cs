using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;

namespace ControlR.Libraries.Viewer.Avalonia.Rendering;


public sealed class SkiaBitmapDrawOperation(Rect bounds, SKBitmap bitmap) : ICustomDrawOperation
{
  private readonly SKBitmap _bitmap = bitmap;

  public Rect Bounds { get; } = bounds;

  // This is called when the render operation is finished and the operation can be cleaned up.
  public void Dispose()
  {
    _bitmap.Dispose();
  }

  public bool Equals(ICustomDrawOperation? other) => this == other;

  // HitTest and Equals are often simplified for a purely visual operation
  public bool HitTest(Point p) => false;
  // This is called by Avalonia's rendering pipeline
  public void Render(ImmediateDrawingContext context)
  {
    // 1. Get the Skia-specific feature from the drawing context
    var leaseFeature = context.PlatformImpl.GetFeature<ISkiaSharpApiLeaseFeature>();

    if (leaseFeature != null)
    {
      // 2. Lease the SKCanvas. This is critical for thread-safety and resource management.
      using var lease = leaseFeature.Lease();
      SKCanvas canvas = lease.SkCanvas;

      // Create the destination rectangle for drawing the bitmap (in Avalonia pixels)
      var destRect = SKRect.Create(
          (float)Bounds.X,
          (float)Bounds.Y,
          (float)Bounds.Width,
          (float)Bounds.Height);

      // 3. Draw the SKBitmap onto the SKCanvas
      canvas.DrawBitmap(_bitmap, destRect);
    }
  }
}
