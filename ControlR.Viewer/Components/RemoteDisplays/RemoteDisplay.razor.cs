using ControlR.Viewer.Enums;
using ControlR.Viewer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using FocusEventArgs = Microsoft.AspNetCore.Components.Web.FocusEventArgs;
using TouchEventArgs = Microsoft.AspNetCore.Components.Web.TouchEventArgs;

namespace ControlR.Viewer.Components.RemoteDisplays;

public partial class RemoteDisplay : IAsyncDisposable
{
    private readonly CancellationTokenSource _componentClosing = new();
    private readonly SemaphoreSlim _typeLock = new(1, 1);
    private readonly string _videoId = $"video-{Guid.NewGuid()}";
    private DotNetObjectReference<RemoteDisplay>? _componentRef;
    private ControlMode _controlMode = ControlMode.Mouse;
    private DisplayDto[] _displays = [];
    private IceServer[] _iceServers = [];
    private bool _isMobileActionsMenuOpen;
    private bool _isScrollModeEnabled;
    private bool _isStreamLoaded;
    private bool _isStreamReady;
    private double _lastPinchDistance = -1;
    private ElementReference _screenArea;
    private DisplayDto? _selectedDisplay;
    private string _statusMessage = "Starting remote control session";
    private double _statusProgress = -1;
    private double _videoHeight;
    private ElementReference _videoRef;
    private double _videoScale = 1;
    private double _videoWidth;
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
    public required IMessenger Messenger { get; init; }

    [Parameter, EditorRequired]
    public required RemoteControlSession Session { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; init; }

    [Inject]
    public required IUiThread UiThread { get; init; }
    [Inject]
    public required IViewerHubConnection ViewerHub { get; init; }
    [Inject]
    public required IDeviceContentWindowStore WindowStore { get; init; }

    private string VideoClasses
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
    private string VideoStyle
    {
        get
        {
            return _isStreamLoaded ?
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
        await JsModule.InvokeVoidAsync("dispose", _videoId);
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
    public async Task NotifyConnectionLost(bool shouldReconnect)
    {
        _isStreamReady = false;
        _isStreamLoaded = false;
        _statusProgress = -1;
        if (shouldReconnect)
        {
            await SetStatusMessage("Creating new streaming session");
            Session.CreateNewSessionId();
            await RequestStreamingSessionFromAgent();
        }
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task NotifyStreamLoaded()
    {
        _isStreamLoaded = true;
        _statusMessage = string.Empty;
        _statusProgress = 0;

        Messenger.Unregister<LocalClipboardChangedMessage>(this);
        Messenger.Register<LocalClipboardChangedMessage>(this, HandleLocalClipboardChanged);

        await ClipboardManager.Start();
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task NotifyStreamReady()
    {
        _isStreamReady = true;
        if (!_isStreamLoaded)
        {
            _statusMessage = "Stream ready";
        }
        _statusProgress = 0;
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task SendIceCandidate(string iceCandidateJson)
    {
        await ViewerHub.SendIceCandidate(Session.StreamerConnectionId, iceCandidateJson);
    }

    [JSInvokable]
    public async Task SendRtcDescription(RtcSessionDescription sessionDescription)
    {
        await InvokeAsync(StateHasChanged);
        await ViewerHub.SendRtcSessionDescription(Session.StreamerConnectionId, sessionDescription);
    }

    [JSInvokable]
    public async Task SetClipboardText(string text)
    {
        Snackbar.Add("Clipboard synced (incoming)", Severity.Info);
        await ClipboardManager.SetText(text);
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
            Logger.LogInformation("Getting ICE servers.");
            await SetStatusMessage("Getting ICE servers");

            var iceServersResult = await ViewerHub.GetIceServers();

            if (!iceServersResult.IsSuccess || iceServersResult.Value.Length == 0)
            {
                Snackbar.Add("Failed to get ICE servers", Severity.Error);
                await Close();
                return;
            }

            _iceServers = iceServersResult.Value;
            await JsModule.InvokeVoidAsync("initialize", _componentRef, _videoId, _iceServers);

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
        Messenger.Register<IceCandidateMessage>(this, HandleIceCandidateReceived);
        Messenger.Register<RtcSessionDescriptionMessage>(this, HandleRtcSessionDescription);
        Messenger.Register<DesktopChangedMessage>(this, HandleDesktopChanged);
        Messenger.Register<StreamerInitDataReceivedMessage>(this, HandleStreamerInitDataReceived);
        Messenger.RegisterGenericMessage(this, HandleParameterlessMessage);

        return base.OnInitializedAsync();
    }

    private async Task ChangeDisplays(DisplayDto display)
    {
        try
        {
            _selectedDisplay = display;
            await JsModule.InvokeVoidAsync("changeDisplays", _videoId, display.MediaId, display.Name);
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

    private async Task HandleDesktopChanged(object recipient, DesktopChangedMessage message)
    {
        if (message.SessionId != Session.SessionId)
        {
            return;
        }

        _isStreamReady = false;
        _isStreamLoaded = false;
        _statusProgress = -1;

        await SetStatusMessage("Switching desktops");

        await ViewerHub.CloseStreamingSession(Session.StreamerConnectionId);

        Session.CreateNewSessionId();

        await JsModule.InvokeVoidAsync("resetPeerConnection", _iceServers, _videoId);

        await RequestStreamingSessionFromAgent();
    }

    private async Task HandleIceCandidateReceived(object recipient, IceCandidateMessage message)
    {
        if (message.SessionId != Session.SessionId)
        {
            return;
        }

        try
        {
            await JsModule.InvokeVoidAsync("receiveIceCandidate", message.CandidateJson, _videoId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while invoking JavaScript function: {name}", "receiveIceCandidate");
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
            await JsModule.InvokeVoidAsync("sendClipboardText", message.Text, _videoId);
            await InvokeAsync(StateHasChanged);
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

    private async Task HandlePlayButtonClicked()
    {
        await JsModule.InvokeVoidAsync("playVideo", _videoRef);
    }

    private async Task HandleRtcSessionDescription(object recipient, RtcSessionDescriptionMessage message)
    {
        if (message.SessionId != Session.SessionId)
        {
            return;
        }

        try
        {
            await JsModule.InvokeVoidAsync("receiveRtcSessionDescription", message.SessionDescription, _videoId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while invoking JavaScript function: {name}", "receiveRtcSessionDescription");
        }
    }

    private void HandleScrollModeToggled(bool isEnabled)
    {
        _isScrollModeEnabled = isEnabled;
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

    private Task HandleStreamerInitDataReceived(object subscriber, StreamerInitDataReceivedMessage message)
    {
        var data = message.StreamerInitData;

        if (data.SessionId != Session.SessionId)
        {
            return Task.CompletedTask;
        }

        Session.StreamerConnectionId = data.StreamerConnectionId;

        _displays = data.Displays ?? [];
        _selectedDisplay = _displays.FirstOrDefault();
        if (_selectedDisplay is not null)
        {
            _videoWidth = _selectedDisplay.Width;
            _videoHeight = _selectedDisplay.Height;
        }

        return Task.CompletedTask;
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

            _videoScale = Math.Max(.25, Math.Min(_videoScale + pinchChange / 100, 3));

            var newWidth = _selectedDisplay.Width * _videoScale;
            var widthChange = newWidth - _videoWidth;
            _videoWidth = newWidth;

            var newHeight = _selectedDisplay.Height * _videoScale;
            var heightChange = newHeight - _videoHeight;
            _videoHeight = newHeight;

            _lastPinchDistance = pinchDistance;
            await InvokeAsync(StateHasChanged);

            var pinchCenterX = (ev.Touches[0].ScreenX + ev.Touches[1].ScreenX) / 2;
            var pinchCenterY = (ev.Touches[0].ScreenY + ev.Touches[1].ScreenY) / 2;

            await JsModule.InvokeVoidAsync("scrollTowardPinch",
                pinchCenterX,
                pinchCenterY,
                _screenArea,
                _videoRef,
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
            await JsModule.InvokeVoidAsync("sendKeyPress", args.Key, _videoId);
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
    private async Task TypeText(string text)
    {
        await _typeLock.WaitAsync();
        try
        {
            await JsModule.InvokeVoidAsync("typeText", text, _videoId);
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