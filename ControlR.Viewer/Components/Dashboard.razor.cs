#if ANDROID

using ControlR.Viewer.Platforms.Android;
using ControlR.Viewer.Services.Android;
using ControlR.Viewer.Platforms.Android.Extensions;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Android;
using Android.Content.PM;

#endif

using Bitbound.SimpleMessenger;
using ControlR.Shared.Dtos;
using ControlR.Shared.Enums;
using ControlR.Shared.Helpers;
using ControlR.Viewer.Components.Devices;
using ControlR.Viewer.Extensions;
using ControlR.Viewer.Models;
using ControlR.Viewer.Models.Messages;
using ControlR.Viewer.Services;
using ControlR.Viewer.Services.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Text.Json;

namespace ControlR.Viewer.Components;

public partial class Dashboard
{
    private readonly Dictionary<string, SortDefinition<DeviceDto>> _sortDefinitions = new()
    {
        ["IsOnline"] = new SortDefinition<DeviceDto>(nameof(DeviceDto.IsOnline), true, 0, x => x.IsOnline),
        ["Name"] = new SortDefinition<DeviceDto>(nameof(DeviceDto.Name), false, 1, x => x.Name)
    };

    private bool _loading = true;

    private string? _searchText;

    [Inject]
    public required IAppState AppState { get; init; }

    [Inject]
    public required IBrowser Browser { get; init; }

    [Inject]
    public required IClipboard Clipboard { get; init; }

    [Inject]
    public required IDeviceCache DeviceCache { get; init; }

    [Inject]
    public required IDialogService DialogService { get; init; }

    [Inject]
    public required ILocalProxyViewer LocalProxy { get; init; }

    [Inject]
    public required ILogger<Dashboard> Logger { get; init; }

    [Inject]
    public required IMessenger Messenger { get; init; }

    [Inject]
    public required IRdpLauncher RdpLauncher { get; init; }

    [Inject]
    public required ISettings Settings { get; init; }

    [Inject]
    public required ISnackbar Snackbar { get; init; }

    [Inject]
    public required IViewerHubConnection ViewerHub { get; init; }

    [Inject]
    public required IDeviceContentWindowStore WindowStore { get; init; }

    private IEnumerable<DeviceDto> FilteredDevices
    {
        get
        {
            if (!Settings.HideOfflineDevices)
            {
                return DeviceCache.Devices;
            }

            return DeviceCache.Devices.Where(x => x.IsOnline);
        }
    }

    private Func<DeviceDto, bool> QuickFilter => x =>
        {
            if (string.IsNullOrWhiteSpace(_searchText))
            {
                return true;
            }

            return JsonSerializer.Serialize(x).Contains(_searchText, StringComparison.OrdinalIgnoreCase);
        };

    protected override async Task OnInitializedAsync()
    {
        using var _ = AppState.IncrementBusyCounter();
        Messenger.RegisterGenericMessage(this, HandleGenericMessage);

        await base.OnInitializedAsync();

        _loading = false;
    }

    private void BroadcastProxyStopRequest(object? sender, EventArgs e)
    {
        Messenger.SendGenericMessage(GenericMessageKind.LocalProxyStopRequested);
    }

    private async Task HandleGenericMessage(GenericMessageKind kind)
    {
        switch (kind)
        {
            case GenericMessageKind.DevicesCacheUpdated:
                {
                    Debouncer.Debounce(TimeSpan.FromSeconds(1), () => InvokeAsync(StateHasChanged));
                }
                break;

            case GenericMessageKind.HubConnectionStateChanged:
                {
                    if (ViewerHub.ConnectionState == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected)
                    {
                        await Refresh();
                    }
                }
                break;

            default:
                break;
        }
    }

    private async Task HideOfflineDevicesChanged(bool isChecked)
    {
        Settings.HideOfflineDevices = isChecked;
        await InvokeAsync(StateHasChanged);
    }

    private async Task Refresh()
    {
        using var _ = AppState.IncrementBusyCounter();
        await DeviceCache.SetAllOffline();
        await InvokeAsync(StateHasChanged);
        await ViewerHub.RequestDeviceUpdates();
        Snackbar.Add("Device refresh requested", Severity.Success);
    }

    private async Task RemoveDevice(DeviceDto device)
    {
        await DeviceCache.Remove(device);
        Snackbar.Add("Device removed.", Severity.Success);
    }

    private async Task RestartDevice(DeviceDto device)
    {
        var result = await DialogService.ShowMessageBox(
            "Confirm Restart",
            $"Are you sure you want to restart {device.Name}?",
            "Yes",
            "No");

        if (result != true)
        {
            return;
        }

        await ViewerHub.SendPowerStateChange(device, PowerStateChangeType.Restart);
    }

    private async Task ShutdownDevice(DeviceDto device)
    {
        var result = await DialogService.ShowMessageBox(
           "Confirm Shutdown",
           $"Are you sure you want to shut down {device.Name}?",
           "Yes",
           "No");

        if (result != true)
        {
            return;
        }

        await ViewerHub.SendPowerStateChange(device, PowerStateChangeType.Shutdown);
    }

    private async Task StartNoVnc(DeviceDto device)
    {
        try
        {
            var sessionId = Guid.NewGuid();
            var vncPassword = RandomGenerator.GenerateString(8);

            Logger.LogInformation("Creating VNC session");
            Snackbar.Add("Requesting VNC session", Severity.Info);

            var vncSessionResult = await ViewerHub.GetVncSession(device.ConnectionId, sessionId, vncPassword);

            if (!vncSessionResult.SessionCreated)
            {
                Snackbar.Add("Failed to acquire VNC session.", Severity.Error);
                return;
            }

            var targetUrl = $"{Settings.ServerUri}/novnc/vnc.html?path=viewer-proxy/{sessionId}&show_dot=true";

            if (vncSessionResult.AutoRunUsed == true)
            {
                targetUrl += $"&password={vncPassword}&autoconnect=true";
            }

            await Browser.OpenAsync(targetUrl);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while requesting VNC session.");
            Snackbar.Add("An error occurred while requesting a VNC session", Severity.Error);
        }
    }

    private async Task StartRdp(DeviceDto device)
    {
        try
        {
            if (device.Platform != SystemPlatform.Windows)
            {
                Snackbar.Add("Only available on Windows", Severity.Warning);
                return;
            }

            Logger.LogInformation("Creating RDP session");
            Snackbar.Add("Requesting RDP session", Severity.Info);

            var sessionId = Guid.NewGuid();

            var rdpProxyResult = await ViewerHub.StartRdpProxy(device.ConnectionId, sessionId);

            if (!rdpProxyResult.IsSuccess)
            {
                Snackbar.Add("Failed to start RDP proxy session.", Severity.Error);
                return;
            }

#if ANDROID
            var hasNotifyPermission = await MainActivity.Current.VerifyNotificationPermissions();
            if (!hasNotifyPermission)
            {
                Snackbar.Add("Notification permission required", Severity.Warning);
                return;
            }

            MainActivity.Current.StartForegroundServiceCompat<ProxyForegroundService>(ProxyForegroundService.ActionStartProxy);
#endif

            var startResult = await LocalProxy.ListenForLocalConnections(sessionId, Settings.LocalProxyPort);

            if (!startResult.IsSuccess)
            {
                Snackbar.Add(startResult.Reason, Severity.Error);
                return;
            }

            var launchResult = await RdpLauncher.LaunchRdp(Settings.LocalProxyPort);
            if (!launchResult.IsSuccess)
            {
                Snackbar.Add(launchResult.Reason, Severity.Error);
            }

#if ANDROID
            MainPage.Current.Window.Activated -= BroadcastProxyStopRequest;
            MainPage.Current.Window.Activated += BroadcastProxyStopRequest;
#endif
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while starting RDP session.");
            Snackbar.Add("An error occurred while RDP session", Severity.Error);
        }
    }

    private void StartTerminal(DeviceDto device)
    {
        try
        {
            var terminalId = Guid.NewGuid();

            void RenderTerminal(RenderTreeBuilder builder)
            {
                builder.OpenComponent<Terminal>(0);
                builder.AddComponentParameter(1, nameof(Terminal.Device), device);
                builder.AddComponentParameter(2, nameof(Terminal.Id), terminalId);
                builder.CloseComponent();
            }
            var contentInstance = new DeviceContentInstance(device, RenderTerminal, "Terminal");
            WindowStore.AddOrUpdate(contentInstance);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while starting terminal session.");
            Snackbar.Add("An error occurred while starting the terminal", Severity.Error);
        }
    }
}