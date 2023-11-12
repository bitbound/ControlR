using Bitbound.SimpleMessenger;
using ControlR.Shared.Services;
using ControlR.Viewer.Enums;
using ControlR.Viewer.Models;
using ControlR.Viewer.Models.Messages;
using ControlR.Viewer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using System.Runtime.Versioning;

namespace ControlR.Viewer.Components.Devices;

[SupportedOSPlatform("browser")]
public partial class DeviceContentWindow : IAsyncDisposable
{
    private WindowState _windowState = WindowState.Maximized;

    [Inject]
    public required IAppState AppState { get; init; }

    [Parameter, EditorRequired]
    public required DeviceContentInstance ContentInstance { get; init; }

    [Inject]
    public required IEnvironmentHelper EnvironmentHelper { get; init; }

    [Inject]
    public required IJSRuntime JsRuntime { get; init; }

    [Inject]
    public required ILogger<DeviceContentWindow> Logger { get; init; }

    [Inject]
    public required IMessenger Messenger { get; init; }

    [Inject]
    public required ISnackbar Snackbar { get; init; }

    [Inject]
    public required IViewerHubConnection ViewerHub { get; init; }

    public ValueTask DisposeAsync()
    {
        //await ViewerHub.CloseStreamingSession(Session.SessionId);
        Messenger.UnregisterAll(this);
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    protected override Task OnInitializedAsync()
    {
        Messenger.Register<DeviceContentWindowStateMessage>(this, HandleDeviceContentWindowStateChanged);

        return base.OnInitializedAsync();
    }

    private async Task Close()
    {
        AppState.DeviceContentWindows.Remove(ContentInstance);
        await DisposeAsync();
    }

    private async Task HandleDeviceContentWindowStateChanged(DeviceContentWindowStateMessage message)
    {
        if (message.WindowId == ContentInstance.WindowId)
        {
            return;
        }

        if (message.State != WindowState.Minimized)
        {
            _windowState = WindowState.Minimized;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void SetWindowState(WindowState state)
    {
        _windowState = state;
        Messenger.Send(new DeviceContentWindowStateMessage(ContentInstance.WindowId, state));
    }
}