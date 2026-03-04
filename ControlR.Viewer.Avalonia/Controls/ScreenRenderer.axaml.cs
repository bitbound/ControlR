using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace ControlR.Viewer.Avalonia.Controls;


public partial class ScreenRenderer : UserControl
{
  private IRemoteDisplayViewModel? _currentViewModel;

  public ScreenRenderer()
  {
    InitializeComponent();
    DataContextChanged += HandleDataContextChanged;
  }

  private IRemoteDisplayViewModel? ViewModel => DataContext as IRemoteDisplayViewModel;

  public Rect GetDisplayRenderBounds(
    double selectedDisplayWidth,
    double selectedDisplayHeight,
    ViewMode viewMode)
  {
    return viewMode switch
    {
      ViewMode.Fit => GetFitSize(selectedDisplayWidth, selectedDisplayHeight),
      ViewMode.Stretch => new Rect(0, 0, Bounds.Width, Bounds.Height),
      ViewMode.Scale => GetScaleSize(selectedDisplayWidth, selectedDisplayHeight),
      _ => throw new InvalidOperationException($"Unsupported view mode: {viewMode}")
    };
  }

  public override void Render(DrawingContext context)
  {
    base.Render(context);

    if (ViewModel is null)
    {
      return;
    }

    try
    {
      var drawBounds = GetDisplayRenderBounds(
        ViewModel.SelectedDisplayWidth,
        ViewModel.SelectedDisplayHeight,
        ViewModel.ViewMode);

      var drawOp = new SkiaBitmapDrawOperation(drawBounds, ViewModel.AcquireCompositedFrame);
      context.Custom(drawOp);
    }
    catch (Exception ex)
    {
      ViewModel.Logger.LogError(ex, "Error drawing bitmap");
    }
  }

  public bool TryGetDisplayPercent(
    Point point,
    double selectedDisplayWidth,
    double selectedDisplayHeight,
    ViewMode viewMode,
    out double percentX,
    out double percentY)
  {
    var renderBounds = GetDisplayRenderBounds(selectedDisplayWidth, selectedDisplayHeight, viewMode);
    if (renderBounds.Width <= 0 || renderBounds.Height <= 0)
    {
      percentX = 0;
      percentY = 0;
      return false;
    }

    var relativeX = (point.X - renderBounds.X) / renderBounds.Width;
    var relativeY = (point.Y - renderBounds.Y) / renderBounds.Height;

    percentX = Math.Clamp(relativeX, 0, 1);
    percentY = Math.Clamp(relativeY, 0, 1);
    return true;
  }

  protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnDetachedFromVisualTree(e);
    UnsubscribeCurrentViewModel();
  }

  private Rect GetFitSize(double selectedDisplayWidth, double selectedDisplayHeight)
  {
    if (selectedDisplayWidth <= 0 || selectedDisplayHeight <= 0)
    {
      return new Rect(0, 0, Bounds.Width, Bounds.Height);
    }

    var widthRatio = Bounds.Width / selectedDisplayWidth;
    var heightRatio = Bounds.Height / selectedDisplayHeight;
    var scale = Math.Min(widthRatio, heightRatio);

    var scaledWidth = selectedDisplayWidth * scale;
    var scaledHeight = selectedDisplayHeight * scale;

    return new Rect(0, 0, scaledWidth, scaledHeight);
  }

  private Rect GetScaleSize(double selectedDisplayWidth, double selectedDisplayHeight)
  {
    var scaledWidth = selectedDisplayWidth * (ViewModel?.RendererScale ?? 1.0);
    var scaledHeight = selectedDisplayHeight * (ViewModel?.RendererScale ?? 1.0);
    return new Rect(0, 0, scaledWidth, scaledHeight);
  }

  private void HandleDataContextChanged(object? sender, EventArgs e)
  {
    UnsubscribeCurrentViewModel();

    _currentViewModel = ViewModel;
    if (_currentViewModel is null)
    {
      return;
    }

    _currentViewModel.FrameQueued += HandleFrameQueued;
  }

  private void HandleFrameQueued(object? sender, EventArgs e)
  {
    Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
  }

  private void UnsubscribeCurrentViewModel()
  {
    if (_currentViewModel is null)
    {
      return;
    }

    _currentViewModel.FrameQueued -= HandleFrameQueued;
    _currentViewModel = null;
  }
}
