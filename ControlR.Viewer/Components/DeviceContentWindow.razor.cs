using ControlR.Viewer.Enums;
using ControlR.Viewer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;

namespace ControlR.Viewer.Components;

public partial class DeviceContentWindow : IAsyncDisposable
{
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

    public WindowState WindowState { get; private set; } = WindowState.Restored;

    [Inject]
    public required IDeviceContentWindowStore WindowStore { get; init; }

    public ValueTask DisposeAsync()
    {
        Messenger.UnregisterAll(this);
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    protected override Task OnInitializedAsync()
    {
        if (EnvironmentHelper.IsMobileDevice)
        {
            WindowState = WindowState.Maximized;
        }

        Messenger.Register<DeviceContentWindowStateMessage>(this, HandleDeviceContentWindowStateChanged);

        return base.OnInitializedAsync();
    }

    private async Task Close()
    {
        WindowStore.Remove(ContentInstance);
        await DisposeAsync();
    }

    private async Task HandleDeviceContentWindowStateChanged(object subscriber, DeviceContentWindowStateMessage message)
    {
        if (message.WindowId == ContentInstance.WindowId)
        {
            return;
        }

        if (message.State != WindowState.Minimized)
        {
            WindowState = WindowState.Minimized;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void SetWindowState(WindowState state)
    {
        WindowState = state;
        Messenger.Send(new DeviceContentWindowStateMessage(ContentInstance.WindowId, state));
    }
}