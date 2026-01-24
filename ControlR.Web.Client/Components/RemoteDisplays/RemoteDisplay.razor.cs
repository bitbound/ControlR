using System.Net.WebSockets;
using System.Runtime.InteropServices.JavaScript;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace ControlR.Web.Client.Components.RemoteDisplays;

public partial class RemoteDisplay : JsInteropableComponent
{
  private readonly string _canvasId = $"canvas-{Guid.NewGuid()}";
  private readonly SemaphoreSlim _typeLock = new(1, 1);

  private string _canvasCssCursor = "default";
  private ElementReference _canvasRef;
  private DotNetObjectReference<RemoteDisplay>? _componentRef;
  private ControlMode _controlMode = ControlMode.Mouse;
  private double _lastPinchDistance = -1;
  private double _lastTouch0X = -1;
  private double _lastTouch0Y = -1;
  private double _lastTouch1X = -1;
  private double _lastTouch1Y = -1;
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
        $"width: {RemoteControlState.CanvasPixelWidth}px; " +
        $"height: {RemoteControlState.CanvasPixelHeight}px;";
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
    if (RemoteControlState.IsViewOnlyEnabled && RemoteControlStream.IsConnected)
    {
      Snackbar.Add("Input is disabled due to view-only mode", Severity.Warning);
      return;
    }
    if (!RemoteControlStream.IsConnected)
    {
      return;
    }

    await RemoteControlStream.SendKeyEvent(key, code, isPressed, ComponentClosing);
  }

  [JSInvokable]
  public async Task SendKeyboardStateReset()
  {
    if (RemoteControlStream.State != WebSocketState.Open)
    {
      return;
    }

    await RemoteControlStream.SendKeyboardStateReset(ComponentClosing);
  }

  [JSInvokable]
  public async Task SendMouseButtonEvent(int button, bool isPressed, double percentX, double percentY)
  {
    if (RemoteControlState.IsViewOnlyEnabled && RemoteControlStream.IsConnected)
    {
      Snackbar.Add("Input is disabled due to view-only mode", Severity.Warning);
      return;
    }

    if (!RemoteControlStream.IsConnected)
    {
      return;
    }

    await RemoteControlStream.SendMouseButtonEvent(button, isPressed, percentX, percentY, ComponentClosing);
  }

  [JSInvokable]
  public async Task SendMouseClick(int button, bool isDoubleClick, double percentX, double percentY)
  {
    if (RemoteControlState.IsViewOnlyEnabled && RemoteControlStream.IsConnected)
    {
      Snackbar.Add("Input is disabled due to view-only mode", Severity.Warning);
      return;
    }

    await RemoteControlStream.SendMouseClick(button, isDoubleClick, percentX, percentY, ComponentClosing);
  }

  [JSInvokable]
  public async Task SendPointerMove(double percentX, double percentY)
  {
    if (RemoteControlState.IsViewOnlyEnabled)
    {
      return;
    }

    await RemoteControlStream.SendPointerMove(percentX, percentY, ComponentClosing);
  }

  [JSInvokable]
  public async Task SendWheelScroll(double percentX, double percentY, double scrollY, double scrollX)
  {
    if (RemoteControlState.IsViewOnlyEnabled)
    {
      return;
    }

    await RemoteControlStream.SendWheelScroll(percentX, percentY, scrollY, scrollX, ComponentClosing);
  }

  protected override async ValueTask DisposeAsync(bool disposing)
  {
    if (disposing)
    {
      Disposer.DisposeAll(_componentRef);
    }
    await base.DisposeAsync(disposing);
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
        await RemoteControlStream.RequestKeyFrame(ComponentClosing);
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

    Disposables.AddRange(
      RemoteControlStream.RegisterMessageHandler(this, HandleRemoteControlDtoReceived),
      RemoteControlState.OnStateChanged(async () =>
      {
        await InvokeAsync(StateHasChanged);
      })
    );

    if (RemoteControlState.IsVirtualKeyboardToggled)
    {
      await _virtualKeyboard.FocusAsync();
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

  private async Task HandleCanvasPointerDown(PointerEventArgs e)
  {
    _controlMode = e.PointerType switch
    {
      "mouse" => ControlMode.Mouse,
      "touch" => ControlMode.Touch,
      _ => _controlMode
    };
  }

  private async Task HandleCanvasPointerMove(PointerEventArgs e)
  {
    if (e.PointerType == "mouse" && _controlMode != ControlMode.Mouse)
    {
      _controlMode = ControlMode.Mouse;
    }

    if (
        e.PointerType == "mouse" &&
        RemoteControlState.IsAutoPanEnabled &&
        RemoteControlState.ViewMode == ViewMode.Scale)
    {
      await JsModule.InvokeVoidAsync("applyAutoPan", _canvasId, _screenArea, e.PageX, e.PageY);
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

      await ClipboardManager.SetText(dto.Text ?? string.Empty);
      Snackbar.Add("Received clipboard text", Severity.Success);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while handling remote clipboard change.");
      Snackbar.Add("Failed to set clipboard text", Severity.Error);
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

  private async Task HandleRemoteControlDtoReceived(DtoWrapper wrapper)
  {
    try
    {
      switch (wrapper.DtoType)
      {
        case DtoType.DisplayData:
          {
            var dto = wrapper.GetPayload<DisplayDataDto>();
            await HandleDisplayDataReceived(dto);
            break;
          }
        case DtoType.ScreenRegion:
          {
            var dto = wrapper.GetPayload<ScreenRegionDto>();
            await DrawRegion(dto);
            break;
          }
        case DtoType.ClipboardText:
          {
            var dto = wrapper.GetPayload<ClipboardTextDto>();
            await HandleClipboardTextReceived(dto);
            break;
          }
        case DtoType.CursorChanged:
          {
            var dto = wrapper.GetPayload<CursorChangedDto>();
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
            // Metrics frame handles this one internally.
            break;
          }
        case DtoType.ToastNotification:
          {
            var dto = wrapper.GetPayload<ToastNotificationDto>();
            Snackbar.Add(dto.Message, dto.Severity.ToMudSeverity());
            break;
          }
        case DtoType.BlockInputResult:
          {
            // Handled in InputPopover.
            break;
          }
        case DtoType.PrivacyScreenResult:
          {
            var dto = wrapper.GetPayload<PrivacyScreenResultDto>();
            RemoteControlState.IsPrivacyScreenEnabled = dto.FinalState;
            await RemoteControlState.NotifyStateChanged();

            if (dto.IsSuccess)
            {
              Snackbar.Add($"Privacy screen {(dto.FinalState ? "enabled" : "disabled")}", Severity.Success);
            }
            else
            {
              Snackbar.Add($"Failed to {(dto.FinalState ? "disable" : "enable")} privacy screen", Severity.Error);
            }
            break;
          }
        case DtoType.SessionDisconnectRequested:
          {
            Snackbar.Add("Remote user requested disconnection", Severity.Info);
            await OnDisconnectRequested.InvokeAsync();
            break;
          }
        default:
          Logger.LogWarning("Received unsupported DTO type: {DtoType}", wrapper.DtoType);
          Snackbar.Add($"Unsupported DTO type: {wrapper.DtoType}", Severity.Warning);
          break;
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while handling remote control DTO. Type: {DtoType}", wrapper.DtoType);
    }
  }

  private void HandleScrollModeToggled(bool isEnabled)
  {
    RemoteControlState.IsScrollModeToggled = isEnabled;
  }

  private async Task HandleVirtualKeyboardBlurred(FocusEventArgs args)
  {
    if (RemoteControlState.IsVirtualKeyboardToggled)
    {
      await _virtualKeyboard.FocusAsync();
    }
  }

  private async Task OnCanvasWheel(WheelEventArgs e)
  {
    try
    {
      await WaitForJsModule(ComponentClosing);

      if (RemoteControlStream.State != WebSocketState.Open)
      {
        return;
      }

      // Handle zoom when both Ctrl and Shift are held
      if (e.ShiftKey && e.CtrlKey && RemoteControlState.SelectedDisplay is not null)
      {
        const double zoomStep = 0.1; // 10% zoom per scroll
        var zoomIn = e.DeltaY < 0;

        var oldWidth = RemoteControlState.CanvasPixelWidth;
        var oldHeight = RemoteControlState.CanvasPixelHeight;

        var newScale = Math.Clamp(
          RemoteControlState.CanvasScale + (zoomIn ? zoomStep : -zoomStep),
          RemoteControlState.MinCanvasScale,
          RemoteControlState.MaxCanvasScale);

        RemoteControlState.CanvasScale = newScale;

        if (RemoteControlState.ViewMode is ViewMode.Fit or ViewMode.Stretch)
        {
          RemoteControlState.ViewMode = ViewMode.Scale;
        }

        var newWidth = RemoteControlState.CanvasPixelWidth;
        var newHeight = RemoteControlState.CanvasPixelHeight;

        await JsModule.InvokeVoidAsync("applyMouseWheelZoom",
          _screenArea,
          _canvasRef,
          newWidth,
          newHeight,
          newWidth - oldWidth,
          newHeight - oldHeight,
          e.OffsetX,
          e.OffsetY);

        await InvokeAsync(StateHasChanged);
        return;
      }

      // Normal scroll - send to remote
      if (RemoteControlState.CanvasPixelWidth > 0 && RemoteControlState.CanvasPixelHeight > 0)
      {
        var percentX = e.OffsetX / RemoteControlState.CanvasPixelWidth;
        var percentY = e.OffsetY / RemoteControlState.CanvasPixelHeight;
        await RemoteControlStream.SendWheelScroll(percentX, percentY, -e.DeltaY, 0, ComponentClosing);
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
      var pinchDistance = MathHelper.GetDistanceBetween(
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

      var oldWidth = RemoteControlState.CanvasPixelWidth;
      var oldHeight = RemoteControlState.CanvasPixelHeight;

      if (RemoteControlState.ViewMode is ViewMode.Fit or ViewMode.Stretch)
      {
        RemoteControlState.ViewMode = ViewMode.Scale;
        RemoteControlState.CanvasScale = RemoteControlState.MinCanvasScale;
      }
      else
      {
        var pinchChange = (pinchDistance - _lastPinchDistance) * .5;
        var newScale = RemoteControlState.CanvasScale + pinchChange / 100;
        RemoteControlState.CanvasScale = Math.Clamp(newScale, RemoteControlState.MinCanvasScale, RemoteControlState.MaxCanvasScale);
      }

      var newWidth = RemoteControlState.CanvasPixelWidth;
      var widthChange = newWidth - oldWidth;

      var newHeight = RemoteControlState.CanvasPixelHeight;
      var heightChange = newHeight - oldHeight;

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
        newWidth,
        newHeight,
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
    _controlMode = ControlMode.Touch;
    _lastPinchDistance = -1;
    _lastTouch0X = -1;
    _lastTouch0Y = -1;
    _lastTouch1X = -1;
    _lastTouch1Y = -1;
  }

  private async Task OnVkKeyDown(KeyboardEventArgs args)
  {
    await WaitForJsModule(ComponentClosing);

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
      await RemoteControlStream.SendTypeText(text, ComponentClosing);
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