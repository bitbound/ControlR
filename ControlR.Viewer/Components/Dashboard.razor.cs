#if ANDROID
using ControlR.Viewer.Platforms.Android;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Android;
using Android.Content.PM;
#endif

using ControlR.Viewer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Text.Json;
using ControlR.Viewer.Components.Dialogs;
using ControlR.Viewer.Components.RemoteDisplays;
using ControlR.Libraries.Shared.Services.Http;

namespace ControlR.Viewer.Components;

public partial class Dashboard
{
    private readonly Dictionary<string, SortDefinition<DeviceDto>> _sortDefinitions = new()
    {
        ["IsOnline"] = new SortDefinition<DeviceDto>(nameof(DeviceDto.IsOnline), true, 0, x => x.IsOnline),
        ["Name"] = new SortDefinition<DeviceDto>(nameof(DeviceDto.Name), false, 1, x => x.Name)
    };

    private readonly FunnelLock _stateChangeLock = new(2, 2, 1, 1);
    private Version? _agentReleaseVersion;
    private bool _loading = true;
    private string? _searchText;

    [Inject]
    public required IAppState AppState { get; init; }

    [Inject]
    public required IDeviceCache DeviceCache { get; init; }

    [Inject]
    public required IDialogService DialogService { get; init; }

    [Inject]
    public required ILogger<Dashboard> Logger { get; init; }

    [Inject]
    public required IMessenger Messenger { get; init; }

    [Inject]
    public required ISettings Settings { get; init; }

    [Inject]
    public required ISnackbar Snackbar { get; init; }


    [Inject]
    public required IVersionApi VersionApi { get; init; }

    [Inject]
    public required IViewerHubConnection ViewerHub { get; init; }

    [Inject]
    public required IWakeOnLanService WakeOnLan { get; init; }

    [Inject]
    public required IDeviceContentWindowStore WindowStore { get; init; }

    private IEnumerable<DeviceDto> FilteredDevices
    {
        get
        {
            if (!Settings.HideOfflineDevices || IsHideOfflineDevicesDisabled)
            {
                return DeviceCache.Devices;
            }

            return DeviceCache.Devices.Where(x => x.IsOnline);
        }
    }

    private bool IsHideOfflineDevicesDisabled =>
        !string.IsNullOrWhiteSpace(_searchText);

    private Func<DeviceDto, bool> QuickFilter => x =>
        {
            if (string.IsNullOrWhiteSpace(_searchText))
            {
                return true;
            }

            var element = JsonSerializer.SerializeToElement(x);
            foreach (var property in element.EnumerateObject())
            {
                try
                {
                    if (property.Value.ToString().Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch { }
            }
            return false;
        };

    protected override async Task OnInitializedAsync()
    {
        using var _ = AppState.IncrementBusyCounter();

        await RefreshLatestAgentVersion();

        Messenger.RegisterGenericMessage(this, HandleGenericMessage);
        Messenger.Register<HubConnectionStateChangedMessage>(this, HandleHubConnectionStateChanged);

        await base.OnInitializedAsync();

        _loading = false;
    }

    private async Task ConfigureDeviceSettings(DeviceDto device)
    {
        try
        {
            var settingsResult = await ViewerHub.GetAgentAppSettings(device.ConnectionId);
            if (!settingsResult.IsSuccess)
            {
                Snackbar.Add(settingsResult.Reason, Severity.Error);
                return;
            }

            var dialogOptions = new DialogOptions()
            {
                DisableBackdropClick = true,
                FullWidth = true,
                MaxWidth = MaxWidth.Medium
            };

            var parameters = new DialogParameters
            {
                { nameof(AppSettingsEditorDialog.AppSettings), settingsResult.Value },
                { nameof(AppSettingsEditorDialog.Device), device }
            };
            var dialogRef = await DialogService.ShowAsync<AppSettingsEditorDialog>("Agent App Settings", parameters, dialogOptions);
            if (dialogRef is null)
            {
                return;
            }
            var result = await dialogRef.Result;
            if (result?.Data is bool isSuccess && isSuccess)
            {
                Snackbar.Add("Settings saved on device", Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while getting device settings.");
            Snackbar.Add("Failed to get device settings", Severity.Error);
        }
    }

    private async Task HandleGenericMessage(object subscriber, GenericMessageKind kind)
    {
        try
        {
            switch (kind)
            {
                case GenericMessageKind.DevicesCacheUpdated:
                    {
                        using var result = await _stateChangeLock.WaitAsync(AppState.AppExiting);
                        if (result.Value)
                        {
                            await InvokeAsync(StateHasChanged);
                        }
                    }
                    break;
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while handling generic message kind {MessageKind}.", kind);
        }
    }

    private async Task HandleHubConnectionStateChanged(object subscriber, HubConnectionStateChangedMessage message)
    {
        if (ViewerHub.ConnectionState == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected)
        {
            await RefreshLatestAgentVersion();
        }
    }

    private async Task HandleRefreshClicked()
    {
        try
        {
            using var _ = AppState.IncrementBusyCounter();
            await DeviceCache.SetAllOffline();
            await ViewerHub.RequestDeviceUpdates();
            await RefreshLatestAgentVersion();
            Snackbar.Add("Device refresh requested", Severity.Success);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while refreshing the dashboard.");
            Snackbar.Add("Dashboard refresh failed", Severity.Error);
        }

    }

    private async Task HideOfflineDevicesChanged(bool isChecked)
    {
        Settings.HideOfflineDevices = isChecked;
        await InvokeAsync(StateHasChanged);
    }

    private bool IsAgentOutdated(DeviceDto device)
    {
        return _agentReleaseVersion is not null &&
                Version.TryParse(device.AgentVersion, out var agentVersion) &&
                !agentVersion.Equals(_agentReleaseVersion);
    }

    private async Task RefreshLatestAgentVersion()
    {
        var agentVerResult = await VersionApi.GetCurrentAgentVersion();
        if (agentVerResult.IsSuccess)
        {
            _agentReleaseVersion = agentVerResult.Value;
        }
    }

    private async Task RemoveDevice(DeviceDto device)
    {
        var result = await DialogService.ShowMessageBox(
            "Confirm Removal",
            "Are you sure you want to remove this device?",
            "Remove",
            "Cancel");

        if (result != true)
        {
            return;
        }
        await DeviceCache.Remove(device);
        Snackbar.Add("Device removed", Severity.Success);
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

    private async Task UpdateDevice(DeviceDto device)
    {
        Snackbar.Add("Sending update request", Severity.Success);
        await ViewerHub.SendAgentUpdateTrigger(device);
    }
    private async Task RemoteControlClicked(DeviceDto device)
    {
        switch (device.Platform)
        {
            case SystemPlatform.Windows:
                var sessionResult = await ViewerHub.GetWindowsSessions(device);
                if (!sessionResult.IsSuccess)
                {
                    Logger.LogResult(sessionResult);
                    Snackbar.Add("Failed to get Windows sessions", Severity.Warning);
                    return;
                }

                var dialogParams = new DialogParameters() { ["DeviceName"] = device.Name, ["Sessions"] = sessionResult.Value };
                var dialogRef = await DialogService.ShowAsync<WindowsSessionSelectDialog>("Select Target Session", dialogParams);
                var result = await dialogRef.Result;
                if (result.Canceled)
                {
                    return;
                }

                if (result.Data is uint sessionId)
                {
                    var remoteControlSession = new RemoteControlSession(device, (int)sessionId);
                    void RenderRemoteDisplay(RenderTreeBuilder builder)
                    {
                        builder.OpenComponent<RemoteDisplay>(0);
                        builder.AddComponentParameter(1, nameof(RemoteDisplay.Session), remoteControlSession);
                        builder.CloseComponent();
                    }
                    var contentInstance = new DeviceContentInstance(device, RenderRemoteDisplay, "Remote");
                    WindowStore.Add(contentInstance);
                }
                break;
            default:
                Snackbar.Add("Platform is not supported", Severity.Warning);
                break;
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
            WindowStore.Add(contentInstance);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while starting terminal session.");
            Snackbar.Add("An error occurred while starting the terminal", Severity.Error);
        }
    }

    private async Task WakeDevice(DeviceDto device)
    {
        if (device.MacAddresses is null ||
            device.MacAddresses.Length == 0)
        {
            Snackbar.Add("No MAC addresses on device", Severity.Warning);
            return;
        }

        await WakeOnLan.WakeDevices(device.MacAddresses);
        await ViewerHub.SendWakeDevice(device.MacAddresses);
        Snackbar.Add("Wake command sent", Severity.Success);
    }
}