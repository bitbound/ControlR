using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Viewer.Enums;
using ControlR.Viewer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using System.Net.WebSockets;
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
    private DotNetObjectReference<RemoteDisplay>? _componentRef;
    private ControlMode _controlMode = ControlMode.Mouse;
    private DisplayDto[] _displays = [];
    private bool _isMobileActionsMenuOpen;
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

    [Parameter, EditorRequired]
    public required RemoteControlSession Session { get; set; }

    [Inject]
    public required ISettings Settings { get; init; }

    [Inject]
    public required ISnackbar Snackbar { get; init; }

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
            return _streamStarted ?
                "display: unset;" :
                "display: none;";
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
        _componentClosing.Cancel();
        await ViewerHub.CloseStreamingSession(Session.StreamerConnectionId);
        Messenger.UnregisterAll(this);
        await JsModule.InvokeVoidAsync("dispose", _canvasId);
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
        await ViewerHub.SendKeyboardStateReset(Session.StreamerConnectionId);
    }

    [JSInvokable]
    public async Task SendKeyEvent(string key, bool isPressed)
    {
        await ViewerHub.SendKeyEvent(Session.StreamerConnectionId, key, isPressed);
    }
    [JSInvokable]
    public async Task SendMouseButtonEvent(int button, bool isPressed, double percentX, double percentY)
    {
        await ViewerHub.SendMouseButtonEvent(Session.StreamerConnectionId, button, isPressed, percentX, percentY);
    }

    [JSInvokable]
    public async Task SendPointerMove(double percentX, double percentY)
    {
        await ViewerHub.SendPointerMove(Session.StreamerConnectionId, percentX, percentY);
    }

    [JSInvokable]
    public async Task SendTypeText(string text)
    {
        await ViewerHub.SendTypeText(Session.StreamerConnectionId, text);
    }

    [JSInvokable]
    public async Task SendWheelScroll(double percentX, double percentY, double scrollY, double scrollX)
    {
        await ViewerHub.SendWheelScroll(Session.StreamerConnectionId, percentX, percentY, scrollY, scrollX);
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
        Messenger.Register<StreamerInitDataReceivedMessage>(this, HandleStreamerInitDataReceived);
        Messenger.Register<StreamerDisconnectedMessage>(this, HandleStreamerDisconnected);
        Messenger.Register<DtoReceivedMessage<ClipboardChangeDto>>(this, HandleClipboardChangeReceived);
        Messenger.RegisterGenericMessage(this, HandleParameterlessMessage);

        return base.OnInitializedAsync();
    }

    private async Task ChangeDisplays(DisplayDto display)
    {
        try
        {
            _selectedDisplay = display;
            await ViewerHub.SendChangeDisplaysRequest(Session.StreamerConnectionId, _selectedDisplay.DisplayId);
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

    private async Task DrawRegion(StreamingFrameHeader header, byte[] encodedRegion)
    {
        try
        {
            await JsModule.InvokeVoidAsync(
                "drawFrame",
                _canvasId,
                header.X,
                header.Y,
                header.Width,
                header.Height,
                encodedRegion);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while drawing frame.");
        }
    }

    private async Task HandleClipboardChangeReceived(object subscriber, DtoReceivedMessage<ClipboardChangeDto> message)
    {
        try
        {
            if (message.Dto.SessionId != Session.SessionId)
            {
                return;
            }
            Snackbar.Add("Clipboard synced (incoming)", Severity.Info);
            await ClipboardManager.SetText(message.Dto.Text ?? string.Empty);
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while handling remote clipboard change.");
        }
    }


    private async Task HandleKeyboardToggled()
    {
        _virtualKeyboardToggled = !_virtualKeyboardToggled;
        _isMobileActionsMenuOpen = false;
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
            await ViewerHub.SendClipboardText(Session.StreamerConnectionId, message.Text, Session.SessionId);
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

    private async Task HandleStreamerDisconnected(object subscriber, StreamerDisconnectedMessage message)
    {
        if (message.SessionId != Session.SessionId)
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

    private async Task HandleStreamerInitDataReceived(object subscriber, StreamerInitDataReceivedMessage message)
    {
        var data = message.StreamerInitData;

        if (data.SessionId != Session.SessionId)
        {
            return;
        }

        Session.StreamerConnectionId = data.StreamerConnectionId;

        Messenger.Unregister<LocalClipboardChangedMessage>(this);
        Messenger.Register<LocalClipboardChangedMessage>(this, HandleLocalClipboardChanged);
        await ClipboardManager.Start();

        _displays = data.Displays ?? [];

        if (_displays.Length == 0)
        {
            Snackbar.Add("No displays received", Severity.Error);
            await Close();
            return;
        }
        _selectedDisplay = _displays.FirstOrDefault(x => x.IsPrimary) ?? _displays.First();

        _canvasWidth = _selectedDisplay.Width;
        _canvasHeight = _selectedDisplay.Height;

        StartWebsocketStreaming().Forget();

        _streamStarted = true;
        _statusMessage = string.Empty;
        _statusProgress = -1;
        await InvokeAsync(StateHasChanged);
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
            var streamingSessionResult = await ViewerHub.RequestStreamingSession(
                Session.Device.ConnectionId,
                Session.SessionId,
                Session.InitialSystemSession);

            _statusProgress = -1;

            if (!streamingSessionResult.IsSuccess)
            {
                Snackbar.Add(streamingSessionResult.Reason, Severity.Error);
                await Close();
                return;
            }
            await SetStatusMessage("Connecting");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while requesting streaming session.");
            Snackbar.Add("An error occurred while requesting streaming session", Severity.Error);
        }
    }

    private async Task StartWebsocketStreaming()
    {
        if (!await _streamLock.WaitAsync(0, _componentClosing.Token))
        {
            return;
        }

        try
        {
            var endpoint = new Uri($"{Settings.WebsocketEndpoint}/{Session.SessionId}");
            var ws = new ClientWebSocket();
            await ws.ConnectAsync(endpoint, _componentClosing.Token);
            await ViewerHub.SendReadySignalToStreamer(Session.StreamerConnectionId);

            StreamFromWebsocket(ws).Forget();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while starting websocket stream.");
            Snackbar.Add("An error occurred while starting the stream", Severity.Error);
        }
        finally
        {
            _streamLock.Release();
        }
      
    }

    private async Task StreamFromWebsocket(ClientWebSocket ws)
    {
        using (ws)
        {
            var headerBuffer = new byte[StreamingFrameHeader.Size];
            var imageBuffer = new byte[ushort.MaxValue];


            while (!_componentClosing.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                try
                {
                    if (_selectedDisplay is null)
                    {
                        await Task.Delay(100);
                        continue;
                    }

                    var result = await ws.ReceiveAsync(headerBuffer, _componentClosing.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Logger.LogInformation("Websocket close message received.");
                        break;
                    }

                    var bytesRead = result.Count;

                    if (bytesRead < StreamingFrameHeader.Size)
                    {
                        Logger.LogError("The frame header is incomplete.");
                        Snackbar.Add("Frame header is incomplete", Severity.Error);
                        break;
                    }

                    var header = new StreamingFrameHeader(headerBuffer);

                    using var imageStream = MemoryProvider.GetRecyclableStream();

                    while (imageStream.Position < header.ImageSize)
                    {
                        result = await ws.ReceiveAsync(imageBuffer, _componentClosing.Token);

                        if (result.MessageType == WebSocketMessageType.Close ||
                            result.Count == 0)
                        {
                            Logger.LogWarning("Stream ended before image was complete.");
                            Snackbar.Add("Stream ended before image was complete", Severity.Warning);
                            break;
                        }

                        await imageStream.WriteAsync(imageBuffer.AsMemory(0, result.Count));
                    }

                    await DrawRegion(header, imageStream.ToArray());
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error while pulling from frame source.");
                }
            }

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