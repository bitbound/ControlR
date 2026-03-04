using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ControlR.Viewer.Avalonia.Native;
using Avalonia.Media.Imaging;
using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Viewer.Avalonia.Controls;

public partial class RemoteDisplayView : UserControl
{
  private IDisposable? _keyboardHookRegistration;
  private TopLevel? _topLevel;
  private IRemoteDisplayViewModel? _viewModel;

  public RemoteDisplayView()
  {
    InitializeComponent();
    DataContextChanged += HandleDataContextChanged;
  }

  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);
    _topLevel = TopLevel.GetTopLevel(this);
    UpdateViewModel(DataContext as IRemoteDisplayViewModel);

    AddPlatformHandlers();

    if (_topLevel is not null)
    {
      // Register tunneled key handlers and allow handling of already-handled events so
      // we can intercept Tab before focus navigation occurs.
      _topLevel.AddHandler(KeyDownEvent, RemoteDisplayView_KeyDown, RoutingStrategies.Tunnel, true);
      _topLevel.AddHandler(KeyUpEvent, RemoteDisplayView_KeyUp, RoutingStrategies.Tunnel, true);
      _topLevel.LostFocus += RemoteDisplayView_LostFocus;
    }

    ScreenRenderer.PointerMoved += ScreenRenderer_PointerMoved;
    ScreenRenderer.PointerPressed += ScreenRenderer_PointerPressed;
    ScreenRenderer.PointerReleased += ScreenRenderer_PointerReleased;
    ScreenRenderer.PointerWheelChanged += ScreenRenderer_PointerWheelChanged;

    ApplyCursorFromViewModel();
    FocusRenderer();
  }

  protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnDetachedFromVisualTree(e);

    if (_topLevel is not null)
    {
      _topLevel.RemoveHandler(KeyDownEvent, RemoteDisplayView_KeyDown);
      _topLevel.RemoveHandler(KeyUpEvent, RemoteDisplayView_KeyUp);
      _topLevel.LostFocus -= RemoteDisplayView_LostFocus;
      _topLevel = null;
    }

    RemovePlatformHandlers();
    UpdateViewModel(null);

    ScreenRenderer.PointerMoved -= ScreenRenderer_PointerMoved;
    ScreenRenderer.PointerPressed -= ScreenRenderer_PointerPressed;
    ScreenRenderer.PointerReleased -= ScreenRenderer_PointerReleased;
    ScreenRenderer.PointerWheelChanged -= ScreenRenderer_PointerWheelChanged;
  }

  private static KeyEventModifiersDto CreateModifiersDto(KeyModifiers modifiers)
  {
    return new KeyEventModifiersDto(
      modifiers.HasFlag(KeyModifiers.Control),
      modifiers.HasFlag(KeyModifiers.Shift),
      modifiers.HasFlag(KeyModifiers.Alt),
      modifiers.HasFlag(KeyModifiers.Meta));
  }

  private static string GetBrowserCode(KeyEventArgs e)
  {
    if (e.PhysicalKey != PhysicalKey.None)
    {
      var physicalCode = e.PhysicalKey.ToString();

      if (physicalCode.Length == 1 && char.IsLetter(physicalCode[0]))
      {
        return $"Key{char.ToUpperInvariant(physicalCode[0])}";
      }

      return physicalCode switch
      {
        "Back" => "Backspace",
        "LShift" => "ShiftLeft",
        "RShift" => "ShiftRight",
        "LCtrl" => "ControlLeft",
        "RCtrl" => "ControlRight",
        "LAlt" => "AltLeft",
        "RAlt" => "AltRight",
        "LWin" => "MetaLeft",
        "RWin" => "MetaRight",
        _ => physicalCode,
      };
    }

    return string.Empty;
  }

  private static string GetBrowserKey(KeyEventArgs e)
  {
    if (!string.IsNullOrWhiteSpace(e.KeySymbol) &&
      e.KeySymbol.Length == 1 &&
      !char.IsControl(e.KeySymbol[0]))
    {
      return e.KeySymbol;
    }

    return e.Key switch
    {
      Key.Back => "Backspace",
      Key.Tab => "Tab",
      Key.Enter => "Enter",
      Key.Escape => "Escape",
      Key.Space => " ",
      Key.Delete => "Delete",
      Key.Insert => "Insert",
      Key.Home => "Home",
      Key.End => "End",
      Key.PageUp => "PageUp",
      Key.PageDown => "PageDown",
      Key.Left => "ArrowLeft",
      Key.Right => "ArrowRight",
      Key.Up => "ArrowUp",
      Key.Down => "ArrowDown",
      Key.LeftCtrl or Key.RightCtrl => "Control",
      Key.LeftShift or Key.RightShift => "Shift",
      Key.LeftAlt or Key.RightAlt => "Alt",
      Key.LWin or Key.RWin => "Meta",
      Key.None => string.Empty,
      _ => e.Key.ToString(),
    };
  }

  private static bool IsWindowsMetaKey(Key key)
  {
    return key is Key.LWin or Key.RWin;
  }

  private static bool TryGetMouseButton(PointerPointProperties properties, out int button, out bool isPressed)
  {
    switch (properties.PointerUpdateKind)
    {
      case PointerUpdateKind.LeftButtonPressed:
        button = 0;
        isPressed = true;
        return true;
      case PointerUpdateKind.LeftButtonReleased:
        button = 0;
        isPressed = false;
        return true;
      case PointerUpdateKind.MiddleButtonPressed:
        button = 1;
        isPressed = true;
        return true;
      case PointerUpdateKind.MiddleButtonReleased:
        button = 1;
        isPressed = false;
        return true;
      case PointerUpdateKind.RightButtonPressed:
        button = 2;
        isPressed = true;
        return true;
      case PointerUpdateKind.RightButtonReleased:
        button = 2;
        isPressed = false;
        return true;
      case PointerUpdateKind.XButton1Pressed:
        button = 3;
        isPressed = true;
        return true;
      case PointerUpdateKind.XButton1Released:
        button = 3;
        isPressed = false;
        return true;
      case PointerUpdateKind.XButton2Pressed:
        button = 4;
        isPressed = true;
        return true;
      case PointerUpdateKind.XButton2Released:
        button = 4;
        isPressed = false;
        return true;
      default:
        button = 0;
        isPressed = false;
        return false;
    }
  }

  private void AddPlatformHandlers()
  {
    if (OperatingSystem.IsWindows())
    {
      _keyboardHookRegistration = NativeHelperWindows.InstallKeyboardHook((vkCode, isDown, isSys) =>
      {
        if (_topLevel is Window window && !window.IsActive)
        {
          return false;
        }

        if (!IsVisible || _viewModel is null)
        {
          return false;
        }

        string? key = vkCode switch
        {
          NativeHelperWindows.VK_LWIN => "Meta",
          NativeHelperWindows.VK_RWIN => "Meta",
          _ => null,
        };

        if (key is null)
        {
          return false;
        }

        var code = vkCode == NativeHelperWindows.VK_LWIN ? "MetaLeft" : "MetaRight";
        var modifiers = NativeHelperWindows.GetModifiersFromNative();
        _viewModel.SendKeyEvent(key, code, isDown, CreateModifiersDto(modifiers)).Forget();

        return true;
      });
    }
  }

  private void ApplyCursorFromViewModel()
  {
    var cursorDto = _viewModel?.ActiveCursor;
    if (cursorDto is null)
    {
      ScreenRenderer.Cursor = new Cursor(StandardCursorType.Arrow);
      return;
    }

    if (cursorDto.Cursor == PointerCursor.Custom)
    {
      if (string.IsNullOrWhiteSpace(cursorDto.CustomCursorBase64Png))
      {
        ScreenRenderer.Cursor = new Cursor(StandardCursorType.Arrow);
        return;
      }

      try
      {
        var cursorBytes = Convert.FromBase64String(cursorDto.CustomCursorBase64Png);
        using var stream = new MemoryStream(cursorBytes);
        using var bitmap = new Bitmap(stream);
        ScreenRenderer.Cursor = new Cursor(bitmap, new PixelPoint((int)cursorDto.XHotspot, (int)cursorDto.YHotspot));
        return;
      }
      catch
      {
        ScreenRenderer.Cursor = new Cursor(StandardCursorType.Arrow);
        return;
      }
    }

    ScreenRenderer.Cursor = cursorDto.Cursor switch
    {
      PointerCursor.Hand => new Cursor(StandardCursorType.Hand),
      PointerCursor.Ibeam => new Cursor(StandardCursorType.Ibeam),
      PointerCursor.SizeNs => new Cursor(StandardCursorType.SizeNorthSouth),
      PointerCursor.SizeWe => new Cursor(StandardCursorType.SizeWestEast),
      PointerCursor.SizeNwse => new Cursor(StandardCursorType.TopLeftCorner),
      PointerCursor.SizeNesw => new Cursor(StandardCursorType.TopRightCorner),
      PointerCursor.Wait => new Cursor(StandardCursorType.Wait),
      _ => new Cursor(StandardCursorType.Arrow),
    };
  }

  private void FocusRenderer()
  {
    Dispatcher.UIThread.Post(() => ScreenRenderer.Focus(), DispatcherPriority.Input);
  }

  private async void HandleCtrlAltDelClicked(object? sender, RoutedEventArgs e)
  {
    if (_viewModel is null)
    {
      return;
    }

    await _viewModel.InvokeCtrlAltDel();
  }

  private void HandleDataContextChanged(object? sender, EventArgs e)
  {
    UpdateViewModel(DataContext as IRemoteDisplayViewModel);
  }

  private async void HandleReceiveClipboardClicked(object? sender, RoutedEventArgs e)
  {
    if (_viewModel is null)
    {
      return;
    }

    await _viewModel.RequestClipboardText();
  }

  private async void HandleSendClipboardClicked(object? sender, RoutedEventArgs e)
  {
    if (_viewModel is null)
    {
      return;
    }

    var clipboard = _topLevel?.Clipboard;
    if (clipboard is null)
    {
      await _viewModel.SendClipboardText(string.Empty);
      return;
    }

    var text = await clipboard.GetTextAsync();
    await _viewModel.SendClipboardText(text ?? string.Empty);
  }

  private async void HandleTypeClipboardClicked(object? sender, RoutedEventArgs e)
  {
    if (_viewModel is null)
    {
      return;
    }

    var clipboard = _topLevel?.Clipboard;
    if (clipboard is null)
    {
      await _viewModel.TypeClipboardText(string.Empty);
      return;
    }

    var text = await clipboard.GetTextAsync();
    await _viewModel.TypeClipboardText(text ?? string.Empty);
  }

  private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName == nameof(IRemoteDisplayViewModel.ActiveCursor))
    {
      ApplyCursorFromViewModel();
    }
  }

  private async void RemoteDisplayView_KeyDown(object? sender, KeyEventArgs e)
  {
    if (!IsVisible || _viewModel is null)
    {
      return;
    }

    if (OperatingSystem.IsWindows() && IsWindowsMetaKey(e.Key))
    {
      e.Handled = true;
      return;
    }

    var key = GetBrowserKey(e);

    // A single space is valid.
    if (string.IsNullOrWhiteSpace(key) && key != " ")
    {
      return;
    }

    var code = GetBrowserCode(e);
    var modifiers = CreateModifiersDto(e.KeyModifiers);

    e.Handled = true;
    await _viewModel.SendKeyEvent(key, code, true, modifiers);
  }

  private async void RemoteDisplayView_KeyUp(object? sender, KeyEventArgs e)
  {
    if (!IsVisible || _viewModel is null)
    {
      return;
    }

    if (OperatingSystem.IsWindows() && IsWindowsMetaKey(e.Key))
    {
      e.Handled = true;
      return;
    }

    var key = GetBrowserKey(e);
    if (string.IsNullOrWhiteSpace(key))
    {
      return;
    }

    var code = GetBrowserCode(e);
    var modifiers = CreateModifiersDto(e.KeyModifiers);
    e.Handled = true;
    await _viewModel.SendKeyEvent(key, code, false, modifiers);
  }

  private async void RemoteDisplayView_LostFocus(object? sender, RoutedEventArgs e)
  {
    if (_viewModel is null)
    {
      return;
    }

    await _viewModel.SendKeyboardStateReset();
  }

  private void RemovePlatformHandlers()
  {
    try
    {
      _keyboardHookRegistration?.Dispose();
      _keyboardHookRegistration = null;
    }
    catch
    {
      // Best effort.
    }
  }

  private async void ScreenRenderer_PointerMoved(object? sender, PointerEventArgs e)
  {
    if (!TryGetPointerPercent(e, out var percentX, out var percentY) || _viewModel is null)
    {
      return;
    }

    TryApplyAutoPan(e);

    await _viewModel.SendPointerMove(percentX, percentY);
  }

  private async void ScreenRenderer_PointerPressed(object? sender, PointerPressedEventArgs e)
  {
    if (_viewModel is null || !TryGetPointerPercent(e, out var percentX, out var percentY))
    {
      return;
    }

    FocusRenderer();

    var properties = e.GetCurrentPoint(ScreenRenderer).Properties;
    if (!TryGetMouseButton(properties, out var button, out var isPressed))
    {
      return;
    }

    e.Handled = true;
    await _viewModel.SendMouseButtonEvent(button, isPressed, percentX, percentY);
  }

  private async void ScreenRenderer_PointerReleased(object? sender, PointerReleasedEventArgs e)
  {
    if (_viewModel is null || !TryGetPointerPercent(e, out var percentX, out var percentY))
    {
      return;
    }

    FocusRenderer();

    var properties = e.GetCurrentPoint(ScreenRenderer).Properties;
    if (!TryGetMouseButton(properties, out var button, out var isPressed))
    {
      return;
    }

    e.Handled = true;
    await _viewModel.SendMouseButtonEvent(button, isPressed, percentX, percentY);
  }

  private async void ScreenRenderer_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
  {
    if (_viewModel is null || !TryGetPointerPercent(e, out var percentX, out var percentY))
    {
      return;
    }

    if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
    {
      TryApplyWheelZoom(e);
      return;
    }

    await _viewModel.SendWheelScroll(percentX, percentY, e.Delta.Y * 120, e.Delta.X * 120);
  }

  private bool TryApplyAutoPan(PointerEventArgs e)
  {
    if (_viewModel is null)
    {
      return false;
    }

    if (!_viewModel.IsAutoPanEnabled || _viewModel.ViewMode != ViewMode.Scale)
    {
      return false;
    }

    if (e.Pointer.Type != PointerType.Mouse)
    {
      return false;
    }

    var maxScrollLeft = RendererScrollViewer.Extent.Width - RendererScrollViewer.Viewport.Width;
    var maxScrollTop = RendererScrollViewer.Extent.Height - RendererScrollViewer.Viewport.Height;
    if (maxScrollLeft <= 0 && maxScrollTop <= 0)
    {
      return false;
    }

    var pointer = e.GetPosition(RendererScrollViewer);
    if (RendererScrollViewer.Bounds.Width <= 0 || RendererScrollViewer.Bounds.Height <= 0)
    {
      return false;
    }

    var percentX = pointer.X / RendererScrollViewer.Bounds.Width;
    var percentY = pointer.Y / RendererScrollViewer.Bounds.Height;

    const double edgeZone = 0.15;
    const double activeStart = edgeZone;
    const double activeEnd = 1 - edgeZone;

    var targetX = percentX < activeStart
      ? 0
      : percentX > activeEnd
        ? 1
        : (percentX - activeStart) / (activeEnd - activeStart);

    var targetY = percentY < activeStart
      ? 0
      : percentY > activeEnd
        ? 1
        : (percentY - activeStart) / (activeEnd - activeStart);

    var clampedTargetX = Math.Clamp(targetX, 0, 1);
    var clampedTargetY = Math.Clamp(targetY, 0, 1);

    RendererScrollViewer.Offset = new Vector(
      Math.Max(0, maxScrollLeft) * clampedTargetX,
      Math.Max(0, maxScrollTop) * clampedTargetY);

    return true;
  }

  private void TryApplyWheelZoom(PointerWheelEventArgs e)
  {
    if (_viewModel is null)
    {
      return;
    }

    if (_viewModel.SelectedDisplayWidth <= 0 || _viewModel.SelectedDisplayHeight <= 0)
    {
      return;
    }

    const double zoomStep = 0.1;
    var zoomIn = e.Delta.Y > 0;

    var oldWidth = ScreenRenderer.Bounds.Width;
    var oldHeight = ScreenRenderer.Bounds.Height;

    _viewModel.RendererScale = Math.Clamp(
      _viewModel.RendererScale + (zoomIn ? zoomStep : -zoomStep),
      _viewModel.MinRendererScale,
      _viewModel.MaxRendererScale);

    if (_viewModel.ViewMode is ViewMode.Fit or ViewMode.Stretch)
    {
      _viewModel.IsScaleViewMode = true;
    }

    var newWidth = _viewModel.SelectedDisplayWidth * _viewModel.RendererScale;
    var newHeight = _viewModel.SelectedDisplayHeight * _viewModel.RendererScale;

    if (newWidth <= 0 || newHeight <= 0)
    {
      return;
    }

    var widthChange = newWidth - oldWidth;
    var heightChange = newHeight - oldHeight;

    var cursorPosition = e.GetPosition(ScreenRenderer);
    var cursorPercentX = cursorPosition.X / newWidth;
    var cursorPercentY = cursorPosition.Y / newHeight;

    var scrollByX = widthChange * cursorPercentX;
    var scrollByY = heightChange * cursorPercentY;

    var targetOffset = RendererScrollViewer.Offset + new Vector(scrollByX, scrollByY);
    var maxScrollLeft = Math.Max(0, RendererScrollViewer.Extent.Width - RendererScrollViewer.Viewport.Width);
    var maxScrollTop = Math.Max(0, RendererScrollViewer.Extent.Height - RendererScrollViewer.Viewport.Height);

    RendererScrollViewer.Offset = new Vector(
      Math.Clamp(targetOffset.X, 0, maxScrollLeft),
      Math.Clamp(targetOffset.Y, 0, maxScrollTop));

    e.Handled = true;
  }

  private bool TryGetPointerPercent(PointerEventArgs e, out double percentX, out double percentY)
  {
    percentX = 0;
    percentY = 0;

    if (!IsVisible || _viewModel is null)
    {
      return false;
    }

    var point = e.GetPosition(ScreenRenderer);
    return ScreenRenderer.TryGetDisplayPercent(
      point,
      _viewModel.SelectedDisplayWidth,
      _viewModel.SelectedDisplayHeight,
      _viewModel.ViewMode,
      out percentX,
      out percentY);
  }

  private void UpdateViewModel(IRemoteDisplayViewModel? viewModel)
  {
    if (_viewModel is not null)
    {
      _viewModel.PropertyChanged -= HandleViewModelPropertyChanged;
    }

    _viewModel = viewModel;

    if (_viewModel is not null)
    {
      _viewModel.PropertyChanged += HandleViewModelPropertyChanged;
    }
  }
}