using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using ControlR.Libraries.Shared.Dtos.RemoteControlDtos;
using ControlR.Web.Client.StateManagement.DeviceAccess;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace ControlR.Web.Client.Components.RemoteDisplays;

public partial class RemoteDisplay : JsInteropableComponent
{
  private const double MaxCanvasScale = 3;
  private const double MinCanvasScale = 0.25;

  private readonly string _canvasId = $"canvas-{Guid.NewGuid()}";
  private readonly CancellationTokenSource _componentClosing = new();
  private readonly SemaphoreSlim _typeLock = new(1, 1);

  private string _canvasCssCursor = "default";
  private double _canvasCssHeight;
  private double _canvasCssWidth;
  private ElementReference _canvasRef;
  private double _canvasScale = 1;
  private DotNetObjectReference<RemoteDisplay>? _componentRef;
  private ControlMode _controlMode = ControlMode.Mouse;
  private TimeSpan _currentLatency;
  private double _lastPinchDistance = -1;
  private double _lastTouch0X = -1;
  private double _lastTouch0Y = -1;
  private double _lastTouch1X = -1;
  private double _lastTouch1Y = -1;
  private CaptureMetricsDto? _latestCaptureMetrics;
  private IDisposable? _messageHandlerRegistration;
  private IDisposable? _remoteControlStateChangedToken;
  private ElementReference _screenArea;
  private ElementReference _virtualKeyboard;

  [Inject]
  public required IClipboardManager ClipboardManager { get; init; }
  [Inject]
  public required IDeviceState DeviceState { get; init; }
  [Parameter]
  [EditorRequired]
  public required bool IsVisible { get; set; }
  [Inject]
  public required IJsInterop JsInterop { get; init; }
  [Inject]
  public required ILogger<RemoteDisplay> Logger { get; init; }
  [Parameter]
  [EditorRequired]
  public EventCallback OnDisconnectRequested { get; set; }
  [Inject]
  public required IRemoteControlState RemoteControlState { get; init; }
  [Inject]
  public required IViewerRemoteControlStream RemoteControlStream { get; init; }
  [Inject]
  public required ISnackbar Snackbar { get; init; }
  [Inject]
  public required TimeProvider TimeProvider { get; init; }
  [Inject]
  public required IHubConnection<IViewerHub> ViewerHub { get; init; }

  private string CanvasClasses
  {
    get
    {
      var classNames = $"{RemoteControlState.ViewMode}";
      if (RemoteControlState.IsScrollModeToggled)
      {
        classNames += " scroll-mode";
      }

      return classNames.ToLower();
    }
  }
  private double CanvasHeight => RemoteControlState.SelectedDisplay?.Height ?? 0;
  private string CanvasStyle
  {
    get
    {
      var display = RemoteControlStream.IsConnected
        ? "display: unset;"
        : "display: none;";

      return
        $"{display} " +
        $"cursor: {_canvasCssCursor}; " +
        $"width: {_canvasCssWidth}px; " +
        $"height: {_canvasCssHeight}px;";
    }
  }
  private double CanvasWidth => RemoteControlState.SelectedDisplay?.Width ?? 0;
  private string OuterClass =>
    IsVisible
      ? string.Empty
      : "d-none";
  private string VirtualKeyboardText
  {
    get => string.Empty;
    set => _ = TypeText(value);
  }

  [JSImport("drawFrame", "RemoteDisplay")]
  public static partial Task DrawFrame(
    string canvasId,
    float x,
    float y,
    float width,
    float height,
    [JSMarshalAs<JSType.MemoryView>()]
    ArraySegment<byte> encodedImage);

  public override async ValueTask DisposeAsync()
  {
    await _componentClosing.CancelAsync();

    Disposer.DisposeAll(
      _messageHandlerRegistration,
      _componentRef,
      _remoteControlStateChangedToken);

    await base.DisposeAsync();
    GC.SuppressFinalize(this);
  }
  [JSInvokable]
  public Task LogError(string message)
  {
    Logger.LogError("JS Log: {Message}", message);
    return Task.CompletedTask;
  }
  [JSInvokable]
  public Task LogInfo(string message)
  {
    Logger.LogInformation("JS Log: {Message}", message);
    return Task.CompletedTask;
  }
  [JSInvokable]
  public async Task SendKeyEvent(string key, string? code, bool isPressed)
  {
    if (RemoteControlStream.State != WebSocketState.Open)
    {
      return;
    }

    await RemoteControlStream.SendKeyEvent(key, code, isPressed, _componentClosing.Token);
  }
  [JSInvokable]
  public async Task SendKeyboardStateReset()
  {
    if (RemoteControlStream.State != WebSocketState.Open)
    {
      return;
    }

    await RemoteControlStream.SendKeyboardStateReset(_componentClosing.Token);
  }
  [JSInvokable]
  public async Task SendMouseButtonEvent(int button, bool isPressed, double percentX, double percentY)
  {
    await RemoteControlStream.SendMouseButtonEvent(button, isPressed, percentX, percentY, _componentClosing.Token);
  }
  [JSInvokable]
  public async Task SendMouseClick(int button, bool isDoubleClick, double percentX, double percentY)
  {
    await RemoteControlStream.SendMouseClick(button, isDoubleClick, percentX, percentY, _componentClosing.Token);
  }
  [JSInvokable]
  public async Task SendPointerMove(double percentX, double percentY)
  {
    await RemoteControlStream.SendPointerMove(percentX, percentY, _componentClosing.Token);
  }
  [JSInvokable]
  public async Task SendWheelScroll(double percentX, double percentY, double scrollY, double scrollX)
  {
    await RemoteControlStream.SendWheelScroll(percentX, percentY, scrollY, scrollX, _componentClosing.Token);
  }
  [JSInvokable]
  public async Task SetCurrentDisplay(DisplayDto display)
  {
    RemoteControlState.SelectedDisplay = display;
    await InvokeAsync(StateHasChanged);
  }
  [JSInvokable]
  public async Task SetDisplays(DisplayDto[] displays)
  {
    RemoteControlState.DisplayData = displays;
    await InvokeAsync(StateHasChanged);
  }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    await base.OnAfterRenderAsync(firstRender);

    if (firstRender)
    {
      _componentRef = DotNetObjectReference.Create(this);

      if (OperatingSystem.IsBrowser())
      {
        await ImportJsHost();
      }

      await JsModule.InvokeVoidAsync("initialize", _componentRef, _canvasId);

      if (RemoteControlStream.IsConnected)
      {
        await RemoteControlStream.RequestKeyFrame(_componentClosing.Token);
      }
    }
  }
  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();

    if (CurrentBreakpoint <= Breakpoint.Sm)
    {
      var isTouchScreen = await JsInterop.IsTouchScreen();

      if (isTouchScreen)
      {
        _controlMode = ControlMode.Touch;
      }
    }

    _messageHandlerRegistration = RemoteControlStream.RegisterMessageHandler(this, HandleRemoteControlDtoReceived);

    // The remote control session is already active, and we're switching back to this tab.
    if (RemoteControlState.SelectedDisplay is { } selectedDisplay)
    {
      _canvasCssWidth = selectedDisplay.Width;
      _canvasCssHeight = selectedDisplay.Height;
    }

    if (RemoteControlState.IsVirtualKeyboardToggled)
    {
      await _virtualKeyboard.FocusAsync();
    }

    _remoteControlStateChangedToken = RemoteControlState.OnStateChanged(async () =>
    {
      await InvokeAsync(StateHasChanged);
    });
  }

  private static double GetDistance(double x1, double y1, double x2, double y2)
  {
    var dx = x1 - x2;
    var dy = y1 - y2;
    return Math.Sqrt(dx * dx + dy * dy);
  }

  private async Task ChangeDisplays(DisplayDto display)
  {
    try
    {
      RemoteControlState.SelectedDisplay = display;
      await RemoteControlStream.SendChangeDisplaysRequest(display.DisplayId, _componentClosing.Token);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while changing displays.");
      Snackbar.Add("An error occurred while changing displays", Severity.Error);
    }
  }
  private async Task DrawRegion(ScreenRegionDto dto)
  {
    try
    {
      if (OperatingSystem.IsBrowser())
      {
        var segment = new ArraySegment<byte>(dto.EncodedImage);
        await DrawFrame(_canvasId, dto.X, dto.Y, dto.Width, dto.Height, segment);
      }
      else
      {
        await JsModule.InvokeVoidAsync("drawFrame", _canvasId, dto.X, dto.Y, dto.Width, dto.Height, dto.EncodedImage.ToArray());
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while drawing frame.");
    }
  }
  private async Task HandleClipboardTextReceived(ClipboardTextDto dto)
  {
    try
    {
      if (dto.SessionId != RemoteControlState.CurrentSession?.SessionId)
      {
        return;
      }

      Snackbar.Add("Received clipboard text", Severity.Info);
      await ClipboardManager.SetText(dto.Text ?? string.Empty);
      await InvokeAsync(StateHasChanged);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while handling remote clipboard change.");
    }
  }
  private async Task HandleCursorChanged(CursorChangedDto dto)
  {
    try
    {
      if (dto.SessionId != RemoteControlState.CurrentSession?.SessionId)
      {
        return;
      }

      if (dto.Cursor == PointerCursor.Custom)
      {
        if (string.IsNullOrWhiteSpace(dto.CustomCursorBase64Png))
        {
          Logger.LogWarning("Received custom cursor change with no image data.");
          return;
        }

        _canvasCssCursor =
          $"url(data:image/png;base64,{dto.CustomCursorBase64Png}) {dto.XHotspot} {dto.YHotspot}, auto";
        await InvokeAsync(StateHasChanged);
        return;
      }

      _canvasCssCursor = dto.Cursor switch
      {
        PointerCursor.Hand => "pointer",
        PointerCursor.Ibeam => "text",
        PointerCursor.NormalArrow => "default",
        PointerCursor.SizeNesw => "nesw-resize",
        PointerCursor.SizeNwse => "nwse-resize",
        PointerCursor.SizeWe => "ew-resize",
        PointerCursor.SizeNs => "ns-resize",
        PointerCursor.Wait => "wait",
        _ => "default"
      };

      await InvokeAsync(StateHasChanged);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while handling cursor change.");
    }
  }
  private async Task HandleDisconnectClicked()
  {
    await OnDisconnectRequested.InvokeAsync();
  }
  private async Task HandleDisplayDataReceived(DisplayDataDto dto)
  {
    RemoteControlState.DisplayData = dto.Displays;

    if (RemoteControlState.DisplayData.Length == 0)
    {
      Snackbar.Add("No displays received", Severity.Error);
      await OnDisconnectRequested.InvokeAsync();
      return;
    }

    var selectedDisplay = RemoteControlState.DisplayData
      .FirstOrDefault(x => x.IsPrimary)
      ?? RemoteControlState.DisplayData[0];

    RemoteControlState.SelectedDisplay = selectedDisplay;

    _canvasCssWidth = selectedDisplay.Width;
    _canvasCssHeight = selectedDisplay.Height;

    await InvokeAsync(StateHasChanged);
  }
  private async Task HandleFullscreenClicked()
  {
    await JsInterop.ToggleFullscreen();
  }
  private async Task HandleKeyboardToggled()
  {
    RemoteControlState.IsVirtualKeyboardToggled = !RemoteControlState.IsVirtualKeyboardToggled;
    if (RemoteControlState.IsVirtualKeyboardToggled)
    {
      await _virtualKeyboard.FocusAsync();
    }
    else
    {
      await _virtualKeyboard.MudBlurAsync();
    }
  }
  private void HandleMetricsToggled()
  {
    RemoteControlState.IsMetricsEnabled = !RemoteControlState.IsMetricsEnabled;
  }
  private async Task HandleReceiveClipboardClicked()
  {
    try
    {
      if (RemoteControlState.CurrentSession is null)
      {
        return;
      }

      await RemoteControlStream.RequestClipboardText(RemoteControlState.CurrentSession.SessionId, _componentClosing.Token);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while handling clipboard change.");
      Snackbar.Add("An error occurred while sending clipboard", Severity.Error);
    }
  }
  private async Task HandleRemoteControlDtoReceived(DtoWrapper message)
  {
    try
    {
      switch (message.DtoType)
      {
        case DtoType.DisplayData:
          {
            var dto = message.GetPayload<DisplayDataDto>();
            await HandleDisplayDataReceived(dto);
            break;
          }
        case DtoType.ScreenRegion:
          {
            var dto = message.GetPayload<ScreenRegionDto>();
            await DrawRegion(dto);
            break;
          }
        case DtoType.ClipboardText:
          {
            var dto = message.GetPayload<ClipboardTextDto>();
            await HandleClipboardTextReceived(dto);
            break;
          }
        case DtoType.CursorChanged:
          {
            var dto = message.GetPayload<CursorChangedDto>();
            await HandleCursorChanged(dto);
            break;
          }
        case DtoType.WindowsSessionEnding:
          {
            Snackbar.Add("Remote Windows session ending", Severity.Warning);
            await OnDisconnectRequested.InvokeAsync();
            break;
          }
        case DtoType.WindowsSessionSwitched:
          {
            Snackbar.Add("Remote Windows session switched", Severity.Info);
            break;
          }
        case DtoType.CaptureMetricsChanged:
          {
            var dto = message.GetPayload<CaptureMetricsDto>();
            _latestCaptureMetrics = dto;
            _currentLatency = TimeProvider.GetUtcNow() - dto.Timestamp;
            await InvokeAsync(StateHasChanged);
            break;
          }
        default:
          Logger.LogWarning("Received unsupported DTO type: {DtoType}", message.DtoType);
          Snackbar.Add($"Unsupported DTO type: {message.DtoType}", Severity.Warning);
          break;
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while handling remote control DTO. Type: {DtoType}", message.DtoType);
    }
  }
  private void HandleScrollModeToggled(bool isEnabled)
  {
    RemoteControlState.IsScrollModeToggled = isEnabled;
  }
  private async Task HandleSendClipboardClicked()
  {
    try
    {
      var text = await ClipboardManager.GetText();
      if (string.IsNullOrWhiteSpace(text))
      {
        Snackbar.Add("Clipboard is empty", Severity.Warning);
        return;
      }

      if (RemoteControlState.CurrentSession is null)
      {
        return;
      }

      Snackbar.Add("Sending clipboard", Severity.Info);
      await RemoteControlStream.SendClipboardText(text, RemoteControlState.CurrentSession.SessionId,
        _componentClosing.Token);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending clipboard.");
      Snackbar.Add("An error occurred while sending clipboard", Severity.Error);
    }
  }
  private async Task HandleTypeClipboardClicked()
  {
    try
    {
      var text = await ClipboardManager.GetText();
      if (string.IsNullOrWhiteSpace(text))
      {
        Snackbar.Add("Clipboard is empty", Severity.Warning);
        return;
      }

      Snackbar.Add("Sending clipboard to type", Severity.Info);
      await RemoteControlStream.SendTypeText(text, _componentClosing.Token);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending clipboard.");
      Snackbar.Add("An error occurred while sending clipboard", Severity.Error);
    }
  }
  private async Task HandleVirtualKeyboardBlurred(FocusEventArgs args)
  {
    if (RemoteControlState.IsVirtualKeyboardToggled)
    {
      await _virtualKeyboard.FocusAsync();
    }
  }
  private async Task InvokeCtrlAltDel()
  {
    try
    {
      if (RemoteControlState.CurrentSession is not { } currentSession)
      {
        Snackbar.Add("No active remote session", Severity.Error);
        return;
      }

      var invokeResult = await ViewerHub.Server.InvokeCtrlAltDel(
        DeviceState.CurrentDevice.Id,
        currentSession.TargetProcessId,
        currentSession.DesktopSessionType);

      if (!invokeResult.IsSuccess)
      {
        Snackbar.Add($"Failed to send Ctrl+Alt+Del: {invokeResult.Reason}", Severity.Error);
        return;
      }

      Snackbar.Add("Ctrl+Alt+Del sent successfully", Severity.Success);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending Ctrl+Alt+Del.");
      Snackbar.Add("An error occurred while sending Ctrl+Alt+Del.", Severity.Error);
    }
  }
  private async Task OnCanvasWheel(WheelEventArgs e)
  {
    try
    {
      await WaitForJsModule(_componentClosing.Token);

      if (RemoteControlStream.State != WebSocketState.Open)
      {
        return;
      }

      // Handle zoom when both Ctrl and Shift are held
      if (e.ShiftKey && e.CtrlKey && RemoteControlState.SelectedDisplay is not null)
      {
        const double zoomStep = 0.1; // 10% zoom per scroll
        var zoomIn = e.DeltaY < 0;

        var oldWidth = _canvasCssWidth;
        var oldHeight = _canvasCssHeight;

        var newScale = Math.Clamp(
          _canvasScale + (zoomIn ? zoomStep : -zoomStep),
          MinCanvasScale,
          MaxCanvasScale);

        // Only proceed if scale actually changed
        if (Math.Abs(newScale - _canvasScale) < 0.001)
        {
          return;
        }

        _canvasScale = newScale;
        _canvasCssWidth = RemoteControlState.SelectedDisplay.Width * newScale;
        _canvasCssHeight = RemoteControlState.SelectedDisplay.Height * newScale;

        if (RemoteControlState.ViewMode is ViewMode.Fit or ViewMode.Stretch)
        {
          RemoteControlState.ViewMode = ViewMode.Original;
        }

        await JsModule.InvokeVoidAsync("applyMouseWheelZoom",
          _screenArea,
          _canvasRef,
          _canvasCssWidth,
          _canvasCssHeight,
          _canvasCssWidth - oldWidth,
          _canvasCssHeight - oldHeight,
          e.OffsetX,
          e.OffsetY);

        await InvokeAsync(StateHasChanged);
        return;
      }

      // Normal scroll - send to remote
      if (_canvasCssWidth > 0 && _canvasCssHeight > 0)
      {
        var percentX = e.OffsetX / _canvasCssWidth;
        var percentY = e.OffsetY / _canvasCssHeight;
        await RemoteControlStream.SendWheelScroll(percentX, percentY, -e.DeltaY, 0, _componentClosing.Token);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error handling wheel event.");
    }
  }
  private void OnTouchCancel(TouchEventArgs ev)
  {
    _lastPinchDistance = -1;
    _lastTouch0X = -1;
    _lastTouch0Y = -1;
    _lastTouch1X = -1;
    _lastTouch1Y = -1;
  }
  private void OnTouchEnd(TouchEventArgs ev)
  {
    _lastPinchDistance = -1;
    _lastTouch0X = -1;
    _lastTouch0Y = -1;
    _lastTouch1X = -1;
    _lastTouch1Y = -1;
  }
  private async void OnTouchMove(TouchEventArgs ev)
  {
    try
    {
      if (RemoteControlState.IsScrollModeToggled)
      {
        return;
      }

      if (ev.Touches.Length != 2)
      {
        return;
      }

      if (RemoteControlState.SelectedDisplay is null)
      {
        return;
      }

      var pinchDistance = GetDistance(
        ev.Touches[0].PageX, ev.Touches[0].PageY,
        ev.Touches[1].PageX, ev.Touches[1].PageY);

      if (_lastPinchDistance <= 0)
      {
        _lastPinchDistance = pinchDistance;
        _lastTouch0X = ev.Touches[0].ClientX;
        _lastTouch0Y = ev.Touches[0].ClientY;
        _lastTouch1X = ev.Touches[1].ClientX;
        _lastTouch1Y = ev.Touches[1].ClientY;
        return;
      }

      if (RemoteControlState.ViewMode is ViewMode.Fit or ViewMode.Stretch)
      {
        RemoteControlState.ViewMode = ViewMode.Original;
        _canvasScale = MinCanvasScale;
      }
      else
      {
        var pinchChange = (pinchDistance - _lastPinchDistance) * .5;
        var newScale = _canvasScale + pinchChange / 100;
        _canvasScale = Math.Clamp(newScale, MinCanvasScale, MaxCanvasScale);
      }

      var newWidth = RemoteControlState.SelectedDisplay.Width * _canvasScale;
      var widthChange = newWidth - _canvasCssWidth;
      _canvasCssWidth = newWidth;

      var newHeight = RemoteControlState.SelectedDisplay.Height * _canvasScale;
      var heightChange = newHeight - _canvasCssHeight;
      _canvasCssHeight = newHeight;

      var touch0DeltaX = ev.Touches[0].ClientX - _lastTouch0X;
      var touch0DeltaY = ev.Touches[0].ClientY - _lastTouch0Y;
      var touch1DeltaX = ev.Touches[1].ClientX - _lastTouch1X;
      var touch1DeltaY = ev.Touches[1].ClientY - _lastTouch1Y;

      var scrollDeltaX = -(touch0DeltaX + touch1DeltaX) / 2;
      var scrollDeltaY = -(touch0DeltaY + touch1DeltaY) / 2;

      _lastPinchDistance = pinchDistance;
      _lastTouch0X = ev.Touches[0].ClientX;
      _lastTouch0Y = ev.Touches[0].ClientY;
      _lastTouch1X = ev.Touches[1].ClientX;
      _lastTouch1Y = ev.Touches[1].ClientY;

      await JsModule.InvokeVoidAsync("applyPinchZoom",
        _screenArea,
        _canvasRef,
        _canvasCssWidth,
        _canvasCssHeight,
        widthChange,
        heightChange,
        scrollDeltaX,
        scrollDeltaY);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while handling touchmove event.");
    }
  }
  private void OnTouchStart(TouchEventArgs ev)
  {
    _lastPinchDistance = -1;
    _lastTouch0X = -1;
    _lastTouch0Y = -1;
    _lastTouch1X = -1;
    _lastTouch1Y = -1;
  }
  private async Task OnVkKeyDown(KeyboardEventArgs args)
  {
    await WaitForJsModule(_componentClosing.Token);

    if (RemoteControlStream.State != WebSocketState.Open)
    {
      return;
    }

    // Handle special keys that reliably fire key events on mobile keyboards
    // These keys should use key event simulation rather than text input
    if (args.Key is "Enter" or "Backspace" or "Tab" or "Escape" or
        "ArrowUp" or "ArrowDown" or "ArrowLeft" or "ArrowRight")
    {
      await SendKeyEvent(args.Key, args.Code, true);
      await SendKeyEvent(args.Key, args.Code, false);
    }
  }
  private async Task TypeText(string text)
  {
    await _typeLock.WaitAsync();
    try
    {
      await RemoteControlStream.SendTypeText(text, _componentClosing.Token);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending text to type.");
    }
    finally
    {
      _typeLock.Release();
    }
  }
}