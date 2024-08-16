using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Viewer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using FocusEventArgs = Microsoft.AspNetCore.Components.Web.FocusEventArgs;
using TouchEventArgs = Microsoft.AspNetCore.Components.Web.TouchEventArgs;

namespace ControlR.Viewer.Components.RemoteDisplays;

public partial class RemoteDisplay : IAsyncDisposable
{
    private readonly string _canvasId = $"canvas-{Guid.NewGuid()}";
    private readonly CancellationTokenSource _componentClosing = new();
    private readonly SemaphoreSlim _streamLock = new(1, 1);
    private readonly SemaphoreSlim _typeLock = new(1, 1);
    private double _canvasHeight;
    private ElementReference _canvasRef;
    private double _canvasScale = 1;
    private double _canvasWidth;
    private IDisposable? _clientOnCloseRegistration;
    private DotNetObjectReference<RemoteDisplay>? _componentRef;
    private ControlMode _controlMode = ControlMode.Mouse;
    private DisplayDto[] _displays = [];
    private bool _isDisposed;
    private bool _isScrollModeEnabled;
    private double _lastPinchDistance = -1;
    private ElementReference _screenArea;
    private DisplayDto? _selectedDisplay;
    private string _statusMessage = "Starting remote control session";
    private double _statusProgress = -1;
    private bool _streamStarted;
    private ViewMode _viewMode = ViewMode.Stretch;
    private ElementReference _virtualKeyboard;
    private bool _virtualKeyboardToggled;
    private string _canvasCssCursor = "default";

    [Inject]
    public required IAppState AppState { get; init; }

    [Inject]
    public required IClipboardManager ClipboardManager { get; init; }

    [CascadingParameter]
    public required DeviceContentInstance ContentInstance { get; init; }

    [CascadingParameter]
    public required DeviceContentWindow ContentWindow { get; init; }

    [Inject]
    public required IEnvironmentHelper EnvironmentHelper { get; init; }

    [Inject]
    public required ILogger<RemoteDisplay> Logger { get; init; }

    [Inject]
    public required IMemoryProvider MemoryProvider { get; init; }

    [Inject]
    public required IMessenger Messenger { get; init; }

    [Inject]
    public required IServiceProvider ServiceProvider { get; init; }

    [Parameter, EditorRequired]
    public required RemoteControlSession Session { get; set; }

    [Inject]
    public required ISettings Settings { get; init; }

    [Inject]
    public required ISnackbar Snackbar { get; init; }

    [Inject]
    public required IViewerStreamingClient StreamingClient { get; init; }

    [Inject]
    public required IUiThread UiThread { get; init; }
    [Inject]
    public required IViewerHubConnection ViewerHub { get; init; }

    [Inject]
    public required IDeviceContentWindowStore WindowStore { get; init; }
    private string CanvasClasses
    {
        get
        {
            var classNames = $"{_viewMode} {ContentWindow.WindowState}";
            if (_isScrollModeEnabled)
            {
                classNames += " scroll-mode";
            }
            return classNames.ToLower();
        }
    }
    private string CanvasStyle
    {
        get
        {
            var display = _streamStarted ?
                "display: unset;" :
                "display: none;";

            return $"{display} cursor: {_canvasCssCursor};";
        }
    }

    private string VirtualKeyboardText
    {
        get
        {
            return string.Empty;
        }
        set
        {
            _ = TypeText(value);
        }
    }


    public async ValueTask DisposeAsync()
    {
        _isDisposed = true;
        await StreamingClient.SendCloseStreamingSession(_componentClosing.Token);
        Messenger.UnregisterAll(this);
        await JsModule.InvokeVoidAsync("dispose", _canvasId);
        _componentClosing.Cancel();
        GC.SuppressFinalize(this);
    }

    [JSInvokable]
    public Task LogError(string message)
    {
        Logger.LogError("JS Log: {message}", message);
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task LogInfo(string message)
    {
        Logger.LogInformation("JS Log: {message}", message);
        return Task.CompletedTask;
    }

    [JSInvokable]
    public async Task SendKeyboardStateReset()
    {
        await StreamingClient.SendKeyboardStateReset(_componentClosing.Token);
    }

    [JSInvokable]
    public async Task SendKeyEvent(string key, bool isPressed)
    {
        await StreamingClient.SendKeyEvent(key, isPressed, _componentClosing.Token);
    }

    [JSInvokable]
    public async Task SendMouseButtonEvent(int button, bool isPressed, double percentX, double percentY)
    {
        await StreamingClient.SendMouseButtonEvent(button, isPressed, percentX, percentY, _componentClosing.Token);
    }

    [JSInvokable]
    public async Task SendMouseClick(int button, bool isDoubleClick, double percentX, double percentY)
    {
        await StreamingClient.SendMouseClick(button, isDoubleClick, percentX, percentY, _componentClosing.Token);
    }

    [JSInvokable]
    public async Task SendPointerMove(double percentX, double percentY)
    {
        await StreamingClient.SendPointerMove(percentX, percentY, _componentClosing.Token);
    }

    [JSInvokable]
    public async Task SendTypeText(string text)
    {
        await StreamingClient.SendTypeText(text, _componentClosing.Token);
    }

    [JSInvokable]
    public async Task SendWheelScroll(double percentX, double percentY, double scrollY, double scrollX)
    {
        await StreamingClient.SendWheelScroll(percentX, percentY, scrollY, scrollX, _componentClosing.Token);
    }


    [JSInvokable]
    public async Task SetCurrentDisplay(DisplayDto display)
    {
        _selectedDisplay = display;
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task SetDisplays(DisplayDto[] displays)
    {
        _displays = displays;
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task SetStatusMessage(string message)
    {
        _statusMessage = message;
        await InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            _componentRef = DotNetObjectReference.Create(this);

            await JsModule.InvokeVoidAsync("initialize", _componentRef, _canvasId);

            await SetStatusMessage("Creating streaming session");
            await RequestStreamingSessionFromAgent();
        }
    }

    protected override Task OnInitializedAsync()
    {
        if (EnvironmentHelper.Platform is SystemPlatform.Android or SystemPlatform.IOS)
        {
            _controlMode = ControlMode.Touch;
            _viewMode = ViewMode.Original;
        }

        Messenger.Register<StreamerDownloadProgressMessage>(this, HandleStreamerDownloadProgress);
        Messenger.Register<DtoReceivedMessage<UnsignedPayloadDto>>(this, HandleUnsignedDtoReceived);
        Messenger.RegisterGenericMessage(this, HandleParameterlessMessage);

        return base.OnInitializedAsync();
    }

    private async Task ChangeDisplays(DisplayDto display)
    {
        try
        {
            _selectedDisplay = display;
            await StreamingClient.SendChangeDisplaysRequest(_selectedDisplay.DisplayId, _componentClosing.Token);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while changing displays.");
            Snackbar.Add("An error occurred while changing displays", Severity.Error);
        }
    }

    private async Task Close()
    {
        WindowStore.Remove(ContentInstance);
        await DisposeAsync();
    }

    private async Task DrawRegion(ScreenRegionDto dto)
    {
        try
        {
            if (dto.SessionId != Session.SessionId)
            {
                return;
            }

            await JsModule.InvokeVoidAsync(
                "drawFrame",
                _canvasId,
                dto.X,
                dto.Y,
                dto.Width,
                dto.Height,
                dto.EncodedImage);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while drawing frame.");
        }
    }

    private async Task HandleClipboardChangeReceived(ClipboardChangeDto dto)
    {
        try
        {
            if (dto.SessionId != Session.SessionId)
            {
                return;
            }
            Snackbar.Add("Clipboard synced (incoming)", Severity.Info);
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
            if (dto.SessionId != Session.SessionId)
            {
                return;
            }
            
            _canvasCssCursor = dto.Cursor switch
            {
                WindowsCursor.Hand => "pointer",
                WindowsCursor.Ibeam => "text",
                WindowsCursor.NormalArrow => "default",
                WindowsCursor.SizeNesw => "nesw-resize",
                WindowsCursor.SizeNwse => "nwse-resize",
                WindowsCursor.SizeWe => "ew-resize",
                WindowsCursor.SizeNs => "ns-resize",
                WindowsCursor.Wait => "wait",
                _ => "default"
            };

            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while handling cursor change.");
        }
    }

    private async Task HandleDisplayDataReceived(DisplayDataDto dto)
    {
        if (dto.SessionId != Session.SessionId)
        {
            return;
        }

        Messenger.Unregister<LocalClipboardChangedMessage>(this);
        Messenger.Register<LocalClipboardChangedMessage>(this, HandleLocalClipboardChanged);
        await ClipboardManager.Start();

        _displays = dto.Displays ?? [];

        if (_displays.Length == 0)
        {
            Snackbar.Add("No displays received", Severity.Error);
            await Close();
            return;
        }
        _selectedDisplay = _displays.FirstOrDefault(x => x.IsPrimary) ?? _displays.First();

        _canvasWidth = _selectedDisplay.Width;
        _canvasHeight = _selectedDisplay.Height;

        _streamStarted = true;
        _statusMessage = string.Empty;
        _statusProgress = -1;

        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleKeyboardToggled()
    {
        _virtualKeyboardToggled = !_virtualKeyboardToggled;
        if (_virtualKeyboardToggled)
        {
            await _virtualKeyboard.FocusAsync();
        }
        else
        {
            await _virtualKeyboard.MudBlurAsync();
        }
    }

    private async Task HandleLocalClipboardChanged(object subscriber, LocalClipboardChangedMessage message)
    {
        try
        {
            Snackbar.Add("Clipboard synced (outgoing)", Severity.Info);
            await StreamingClient.SendClipboardText(message.Text, Session.SessionId, _componentClosing.Token);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while handling clipboard change.");
        }
    }

    private async void HandleParameterlessMessage(object sender, GenericMessageKind kind)
    {
        switch (kind)
        {
            case GenericMessageKind.ShuttingDown:
                await DisposeAsync();
                break;

            default:
                break;
        }
    }

    private void HandleScrollModeToggled(bool isEnabled)
    {
        _isScrollModeEnabled = isEnabled;
    }

    private async Task HandleStreamerDisconnected()
    {
        if (_isDisposed)
        {
            return;
        }

        _streamStarted = false;
        _statusProgress = -1;
        await SetStatusMessage("Creating new streaming session");
        Session.CreateNewSessionId();
        await RequestStreamingSessionFromAgent();
        await InvokeAsync(StateHasChanged);
    }
    private async Task HandleStreamerDownloadProgress(object recipient, StreamerDownloadProgressMessage message)
    {
        if (message.StreamingSessionId != Session.SessionId)
        {
            return;
        }

        _statusProgress = message.DownloadProgress;
        _statusMessage = message.Message;

        await InvokeAsync(StateHasChanged);
    }
    private async Task HandleUnsignedDtoReceived(object subscriber, DtoReceivedMessage<UnsignedPayloadDto> message)
    {
        var wrapper = message.Dto;

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
                case DtoType.ClipboardChanged:
                    {
                        var dto = wrapper.GetPayload<ClipboardChangeDto>();
                        await HandleClipboardChangeReceived(dto);
                        break;
                    }
                case DtoType.CursorChanged:
                    {
                        var dto = wrapper.GetPayload<CursorChangedDto>();
                        await HandleCursorChanged(dto);
                        break;
                    }
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while handling unsigned payload. Type: {DtoType}", wrapper.DtoType);
        }
    }

    private async Task HandleVirtualKeyboardBlurred(FocusEventArgs args)
    {
        if (_virtualKeyboardToggled)
        {
            await _virtualKeyboard.FocusAsync();
        }
    }

    private async Task InvokeCtrlAltDel()
    {
        await ViewerHub.InvokeCtrlAltDel(Session.Device.Id);
    }

    private void OnTouchCancel(TouchEventArgs ev)
    {
        _lastPinchDistance = -1;
    }

    private void OnTouchEnd(TouchEventArgs ev)
    {
        _lastPinchDistance = -1;
    }

    private async void OnTouchMove(TouchEventArgs ev)
    {
        try
        {
            if (_isScrollModeEnabled)
            {
                return;
            }

            if (ev.Touches.Length != 2)
            {
                return;
            }

            if (_selectedDisplay is null)
            {
                return;
            }

            var pinchDistance = MathHelper.GetDistanceBetween(
                ev.Touches[0].PageX,
                ev.Touches[0].PageY,
                ev.Touches[1].PageX,
                ev.Touches[1].PageY);

            if (_lastPinchDistance <= 0)
            {
                _lastPinchDistance = pinchDistance;
                return;
            }

            var pinchChange = (pinchDistance - _lastPinchDistance) * .5;

            _viewMode = ViewMode.Original;

            _canvasScale = Math.Max(.25, Math.Min(_canvasScale + pinchChange / 100, 3));

            var newWidth = _selectedDisplay.Width * _canvasScale;
            var widthChange = newWidth - _canvasWidth;
            _canvasWidth = newWidth;

            var newHeight = _selectedDisplay.Height * _canvasScale;
            var heightChange = newHeight - _canvasHeight;
            _canvasHeight = newHeight;

            _lastPinchDistance = pinchDistance;
            await InvokeAsync(StateHasChanged);

            var pinchCenterX = (ev.Touches[0].ScreenX + ev.Touches[1].ScreenX) / 2;
            var pinchCenterY = (ev.Touches[0].ScreenY + ev.Touches[1].ScreenY) / 2;

            await JsModule.InvokeVoidAsync("scrollTowardPinch",
                pinchCenterX,
                pinchCenterY,
                _screenArea,
                _canvasRef,
                newWidth,
                newHeight,
                widthChange,
                heightChange);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while handling touchmove event.");
        }
    }

    private void OnTouchStart(TouchEventArgs ev)
    {
        _lastPinchDistance = -1;
    }

    private async Task OnVkKeyDown(KeyboardEventArgs args)
    {
        await JsModuleReady.Wait(_componentClosing.Token);

        if (args.Key == "Enter" || args.Key == "Backspace")
        {
            await JsModule.InvokeVoidAsync("sendKeyPress", args.Key, _canvasId);
        }
    }
    private async Task RequestStreamingSessionFromAgent()
    {
        try
        {
            Logger.LogInformation("Creating streaming session.");

            var bridgeOrigin = await ViewerHub.GetWebsocketBridgeOrigin();
            var accessKey = RandomGenerator.CreateAccessToken();

            var websocketUri = bridgeOrigin is not null ?
                new Uri(bridgeOrigin, $"/bridge/{Session.SessionId}/{accessKey}") :
                new Uri(Settings.ServerUri, $"/bridge/{Session.SessionId}/{accessKey}").ToWebsocketUri();

            Logger.LogInformation("Resolved WS bridge origin: {BridgeOrigin}", websocketUri.Authority);

            var streamingSessionResult = await ViewerHub.RequestStreamingSession(
                Session.Device.ConnectionId,
                Session.SessionId,
                websocketUri,
                Session.InitialSystemSession);

            _statusProgress = -1;

            if (!streamingSessionResult.IsSuccess)
            {
                Snackbar.Add(streamingSessionResult.Reason, Severity.Error);
                await Close();
                return;
            }
            await SetStatusMessage("Connecting");

            StartWebsocketStreaming(websocketUri).Forget();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while requesting streaming session.");
            Snackbar.Add("An error occurred while requesting streaming session", Severity.Error);
        }
    }

    private async Task StartWebsocketStreaming(Uri websocketUri)
    {
        if (!await _streamLock.WaitAsync(0, _componentClosing.Token))
        {
            return;
        }

        try
        {
            await StreamingClient.Connect(websocketUri, _componentClosing.Token);
            _clientOnCloseRegistration?.Dispose();
            _clientOnCloseRegistration = StreamingClient.OnClose(HandleStreamerDisconnected);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while connecting to websocket endpoint.");
            Snackbar.Add("An error occurred while connecting to streamer", Severity.Error);
            await Close();
        }
        finally
        {
            _streamLock.Release();
        }
    }

    private async Task TypeText(string text)
    {
        await _typeLock.WaitAsync();
        try
        {
            await JsModule.InvokeVoidAsync("typeText", text, _canvasId);
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