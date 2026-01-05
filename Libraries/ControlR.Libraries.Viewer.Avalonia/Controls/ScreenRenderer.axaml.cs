using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.Viewer.Avalonia.Controls;


public partial class ScreenRenderer : UserControl
{

  public ScreenRenderer()
  {
    InitializeComponent();
  }

  private IScreenRendererViewModel? ViewModel => DataContext as IScreenRendererViewModel;

  public override void Render(DrawingContext context)
  {
    base.Render(context);

    if (ViewModel is null)
    {
      return;
    }

    if (!ViewModel.FrameChannel.Reader.TryRead(out var captureFrame) || captureFrame is null)
    {
      return;
    }

    try
    {
      Guard.IsNotNull(captureFrame.Bitmap);
      var drawOp = new SkiaBitmapDrawOperation(Bounds, captureFrame.Bitmap);
      context.Custom(drawOp);

    }
    catch (Exception ex)
    {
      ViewModel.Logger.LogError(ex, "Error drawing bitmap");
    }
  }

  private SKBitmap? DecodeRegion(byte[] encodedImage)
  {
    if (Design.IsDesignMode)
    {
      return null;
    }

    try
    {
      using var imageStream = new MemoryStream(encodedImage);
      return SKBitmap.Decode(imageStream);
    }
    catch (Exception ex)
    {
      ViewModel?.Logger.LogError(ex, "Error in fast screen region processing");
      return null;
    }
  }


  private void HandleCaptureFrameAdded()
  {
    // Only invalidate if we successfully queued a frame
    Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
  }
}
