using Bitbound.SimpleMessenger;
using ControlR.Devices.Common.Extensions;
using ControlR.Shared.Dtos;
using ControlR.Shared.Enums;
using ControlR.Shared.Helpers;
using ControlR.Shared.Models;
using ControlR.Shared.Services;
using ControlR.Viewer.Enums;
using ControlR.Viewer.Extensions;
using ControlR.Viewer.Models;
using ControlR.Viewer.Models.Messages;
using ControlR.Viewer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using System.Runtime.Versioning;
using TouchEventArgs = Microsoft.AspNetCore.Components.Web.TouchEventArgs;

namespace ControlR.Viewer.Components;

public partial class RemoteDisplay : IAsyncDisposable
{
    private readonly SemaphoreSlim _typeLock = new(1, 1);
    private readonly string _videoId = $"video-{Guid.NewGuid()}";
    private DotNetObjectReference<RemoteDisplay>? _componentRef;
    private ElementReference _contentArea;
    private ControlMode _controlMode = ControlMode.Mouse;
    private DisplayDto[] _displays = [];
    private IceServer[] _iceServers = [];
    private bool _isMobileActionsMenuOpen;
    private bool _isStreamLoaded;
    private bool _isStreamReady;
    private double _lastPinchDistance = -1;
    private IJSObjectReference? _module;
    private DisplayDto? _selectedDisplay;
    private string _statusMessage = "Starting remote control session";
    private double _statusProgress = -1;
    private double _videoHeight;
    private ElementReference _videoRef;
    private double _videoScale = 1;
    private double _videoWidth;
    private ViewMode _viewMode = ViewMode.Stretch;
    private ElementReference _virtualKeyboard;
    private WindowState _windowState = WindowState.Maximized;
#nullable disable

    [Parameter, EditorRequired]
    public RemoteControlSession Session { get; set; }

    [Inject]
    private IAppState AppState { get; init; }

    [Inject]
    private IEnvironmentHelper EnvironmentHelper { get; init; }

    [Inject]
    private IJSRuntime JsRuntime { get; init; }

    [Inject]
    private ILogger<RemoteDisplay> Logger { get; init; }

    [Inject]
    private IMessenger Messenger { get; init; }

    [Inject]
    private ISnackbar Snackbar { get; init; }

    private string VideoDisplayCss
    {
        get
        {
            return _isStreamLoaded ?
                "display: unset;" :
                "display: none;";
        }
    }

    private string VideoSizeCss
    {
        get
        {
            if (_viewMode is ViewMode.Fit or ViewMode.Stretch || _videoHeight < 1 || _videoWidth < 1)
            {
                return string.Empty;
            }
            return $"width: {_videoWidth}px; height: {_videoHeight}px;";
        }
    }

    [Inject]
    private IViewerHubConnection ViewerHub { get; init; }

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

#nullable enable

    public async ValueTask DisposeAsync()
    {
        await ViewerHub.CloseStreamingSession(Session.SessionId);
        Messenger.UnregisterAll(this);
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("dispose", _videoId);
        }
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
    public async Task NotifyStreamLoaded()
    {
        _isStreamLoaded = true;
        _statusMessage = string.Empty;
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task NotifyStreamReady()
    {
        _isStreamReady = true;
        _statusMessage = "Stream ready";
        _statusProgress = 0;
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task SendIceCandidate(string iceCandidateJson)
    {
        await ViewerHub.SendIceCandidate(Session.SessionId, iceCandidateJson);
    }

    [JSInvokable]
    public async Task SendRtcDescription(RtcSessionDescription sessionDescription)
    {
        await InvokeAsync(StateHasChanged);
        await ViewerHub.SendRtcSessionDescription(Session.SessionId, sessionDescription);
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
        _module ??= await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./Components/RemoteDisplay.razor.js");

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
            await _module.InvokeVoidAsync("initialize", _componentRef, _videoId, _iceServers);

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

        Messenger.Register<RemoteControlDownloadProgressMessage>(this, HandleRemoteControlDownloadProgress);
        Messenger.Register<IceCandidateMessage>(this, HandleIceCandidateReceived);
        Messenger.Register<RtcSessionDescriptionMessage>(this, HandleRtcSessionDescription);
        Messenger.Register<RemoteDisplayWindowStateMessage>(this, HandleRemoteDisplayWindowStateChanged);
        Messenger.Register<DesktopChangedMessage>(this, HandleDesktopChanged);
        Messenger.RegisterGenericMessage(this, HandleParameterlessMessage);

        return base.OnInitializedAsync();
    }

    private async Task Close()
    {
        AppState.RemoteControlSessions.Remove(Session);
        await DisposeAsync();
    }

    private async Task HandleDesktopChanged(object recipient, DesktopChangedMessage message)
    {
        if (message.SessionId != Session.SessionId || _module is null)
        {
            return;
        }

        await SetStatusMessage("Switching desktops");

        await ViewerHub.CloseStreamingSession(Session.SessionId);

        Session.CreateNewSessionId();

        await _module.InvokeVoidAsync("resetPeerConnection", _iceServers, _videoId);

        await RequestStreamingSessionFromAgent(message.DesktopName);
    }

    private async Task HandleIceCandidateReceived(object recipient, IceCandidateMessage message)
    {
        if (message.SessionId != Session.SessionId || _module is null)
        {
            return;
        }

        try
        {
            await _module.InvokeVoidAsync("receiveIceCandidate", message.CandidateJson, _videoId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while invoking JavaScript function: {name}", "receiveIceCandidate");
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

    private async Task HandleRemoteControlDownloadProgress(object recipient, RemoteControlDownloadProgressMessage message)
    {
        if (message.StreamingSessionId != Session.SessionId)
        {
            return;
        }

        _statusProgress = message.DownloadProgress;

        if (_statusProgress < 1)
        {
            _statusMessage = "Downloading streamer on remote device";
        }
        else
        {
            _statusMessage = "Extracting and starting streamer";
            _statusProgress = -1;
        }
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleRemoteDisplayWindowStateChanged(object recipient, RemoteDisplayWindowStateMessage message)
    {
        if (message.SessionId == Session.SessionId)
        {
            return;
        }

        if (message.State != WindowState.Minimized)
        {
            _windowState = WindowState.Minimized;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task HandleRtcSessionDescription(object recipient, RtcSessionDescriptionMessage message)
    {
        if (message.SessionId != Session.SessionId || _module is null)
        {
            return;
        }

        try
        {
            await _module.InvokeVoidAsync("receiveRtcSessionDescription", message.SessionDescription, _videoId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while invoking JavaScript function: {name}", "receiveRtcSessionDescription");
        }
    }

    private async Task InvokeCtrlAltDel()
    {
        await ViewerHub.InvokeCtrlAltDel(Session.Device.Id);
    }

    private async Task InvokeKeyboard()
    {
        _isMobileActionsMenuOpen = false;
        await _virtualKeyboard.FocusAsync();
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

        var pinchChange = pinchDistance - _lastPinchDistance;

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

        if (_module is null)
        {
            return;
        }

        var pinchCenterX = (ev.Touches[0].ScreenX + ev.Touches[1].ScreenX) / 2;
        var pinchCenterY = (ev.Touches[0].ScreenY + ev.Touches[1].ScreenY) / 2;

        await _module.InvokeVoidAsync("scrollTowardPinch", pinchCenterX, pinchCenterY, _contentArea, widthChange, heightChange);
    }

    private void OnTouchStart(TouchEventArgs ev)
    {
        _lastPinchDistance = -1;
    }

    private async Task OnVkKeyDown(KeyboardEventArgs args)
    {
        if (_module is null)
        {
            return;
        }

        if (args.Key == "Enter" || args.Key == "Backspace")
        {
            await _module.InvokeVoidAsync("sendKeyPress", args.Key, _videoId);
        }
    }

    private async Task RequestStreamingSessionFromAgent(string desktopName = "Default")
    {
        try
        {
            if (_module is null)
            {
                Snackbar.Add("JavaScript services must be initialized before remote control", Severity.Error);
                return;
            }

            Logger.LogInformation("Creating streaming session");
            var streamingSessionResult = await ViewerHub.GetStreamingSession(Session.Device.ConnectionId, Session.SessionId, Session.InitialSystemSession, desktopName);

            _statusProgress = -1;

            if (!streamingSessionResult.IsSuccess)
            {
                Snackbar.Add(streamingSessionResult.Reason, Severity.Error);
                await Close();
                return;
            }

            _displays = streamingSessionResult.Value.Displays;
            _selectedDisplay = _displays?.FirstOrDefault();
            if (_selectedDisplay is not null)
            {
                _videoWidth = _selectedDisplay.Width;
                _videoHeight = _selectedDisplay.Height;
            }

            await SetStatusMessage("Connecting");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while requesting streaming session.");
            Snackbar.Add("An error occurred while requesting streaming session", Severity.Error);
        }
    }

    private void SetWindowState(WindowState state)
    {
        _windowState = state;
        _videoScale = 1;
        Messenger.Send(new RemoteDisplayWindowStateMessage(Session.SessionId, state));
    }

    private async Task TypeText(string text)
    {
        if (_module is null)
        {
            return;
        }

        await _typeLock.WaitAsync();
        try
        {
            await _module.InvokeVoidAsync("typeText", text, _videoId);
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