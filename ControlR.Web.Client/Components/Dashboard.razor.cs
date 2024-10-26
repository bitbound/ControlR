using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace ControlR.Web.Client.Components;

public partial class Dashboard
{
  private readonly Dictionary<string, SortDefinition<DeviceResponseDto>> _sortDefinitions = new()
  {
    ["IsOnline"] = new SortDefinition<DeviceResponseDto>(nameof(DeviceResponseDto.IsOnline), true, 0, x => x.IsOnline),
    ["Name"] = new SortDefinition<DeviceResponseDto>(nameof(DeviceResponseDto.Name), false, 1, x => x.Name)
  };

  private readonly FunnelLock _stateChangeLock = new(2, 2, 1, 1);
  private Version? _agentReleaseVersion;
  private bool _loading = true;
  private string? _searchText;

  [Inject]
  public required IBusyCounter BusyCounter { get; init; }

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
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IViewerHubConnection ViewerHub { get; init; }


  [Inject]
  public required IDeviceContentWindowStore WindowStore { get; init; }

  private bool _hideOfflineDevices;


  private IEnumerable<DeviceResponseDto> FilteredDevices
  {
    get
    {
      if (!_hideOfflineDevices || IsHideOfflineDevicesDisabled)
      {
        return DeviceCache.Devices;
      }

      return DeviceCache.Devices.Where(x => x.IsOnline);
    }
  }

  private bool IsHideOfflineDevicesDisabled =>
      !string.IsNullOrWhiteSpace(_searchText);

  private Func<DeviceResponseDto, bool> QuickFilter => x =>
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
    await base.OnInitializedAsync();

    using var _ = BusyCounter.IncrementBusyCounter();

    _hideOfflineDevices = await Settings.GetHideOfflineDevices();

    await RefreshLatestAgentVersion();

    Messenger.RegisterGenericMessage(this, HandleGenericMessage);
    Messenger.Register<HubConnectionStateChangedMessage>(this, HandleHubConnectionStateChanged);

    if (!DeviceCache.Devices.Any())
    {
      await DeviceCache.Refresh();
    }

    _loading = false;
  }

  private async Task ConfigureDeviceSettings(DeviceResponseDto device)
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
        BackdropClick = false,
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
            _loading = true;
            await RateLimiter
              .Throttle(async () =>
              {
                await InvokeAsync(StateHasChanged);
              },
              TimeSpan.FromSeconds(2));

            Debouncer.Debounce(
              TimeSpan.FromSeconds(1),
              async () => 
              {
                _loading = false;
                await InvokeAsync(StateHasChanged);
              });
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
    if (ViewerHub.IsConnected)
    {
      await RefreshLatestAgentVersion();
    }
  }

  private async Task HandleRefreshClicked()
  {
    try
    {
      _loading = true;
      await InvokeAsync(StateHasChanged);
      using var _ = BusyCounter.IncrementBusyCounter();
      await DeviceCache.Refresh();
      await RefreshLatestAgentVersion();
      Snackbar.Add("Device refresh requested", Severity.Success);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while refreshing the dashboard.");
      Snackbar.Add("Dashboard refresh failed", Severity.Error);
    }
    finally
    {
      _loading = false;
    }
  }

  private async Task HideOfflineDevicesChanged(bool isChecked)
  {
    _hideOfflineDevices = isChecked;
    await Settings.SetHideOfflineDevices(isChecked);
    await InvokeAsync(StateHasChanged);
  }

  private bool IsAgentOutdated(DeviceResponseDto device)
  {
    return _agentReleaseVersion is not null &&
            Version.TryParse(device.AgentVersion, out var agentVersion) &&
            !agentVersion.Equals(_agentReleaseVersion);
  }

  private async Task RefreshLatestAgentVersion()
  {
    var agentVerResult = await ControlrApi.GetCurrentAgentVersion();
    if (agentVerResult.IsSuccess)
    {
      _agentReleaseVersion = agentVerResult.Value;
    }
  }

  private async Task RemoveDevice(DeviceResponseDto device)
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

  private async Task RestartDevice(DeviceResponseDto device)
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
    Snackbar.Add("Restart command sent", Severity.Success);
  }

  private async Task ShutdownDevice(DeviceResponseDto device)
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
    Snackbar.Add("Shutdown command sent", Severity.Success);
  }

  private async Task UpdateDevice(DeviceResponseDto device)
  {
    Snackbar.Add("Sending update request", Severity.Success);
    await ViewerHub.SendAgentUpdateTrigger(device);
  }
  private async Task RemoteControlClicked(DeviceResponseDto device)
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
        if (result is null || result.Canceled)
        {
          return;
        }

        if (result.Data is uint sessionId)
        {
          var remoteControlSession = new RemoteControlSession(device, (int)sessionId);
          WindowStore.AddContentInstance<RemoteDisplay>(
            device, 
            DeviceContentInstanceType.RemoteControl,
            new Dictionary<string, object?>()
            {
              [nameof(RemoteDisplay.Session)] = remoteControlSession
            });
        }
        break;
      default:
        Snackbar.Add("Platform is not supported", Severity.Warning);
        break;
    }

  }


  private void StartTerminal(DeviceResponseDto device)
  {
    try
    {
      var terminalId = Guid.NewGuid();

      WindowStore.AddContentInstance<Terminal>(
        device,
        DeviceContentInstanceType.Terminal,
        new Dictionary<string, object?>()
        {
          [nameof(Terminal.Id)] = terminalId,
          [nameof(Terminal.Device)] = device
        });

    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while starting terminal session.");
      Snackbar.Add("An error occurred while starting the terminal", Severity.Error);
    }
  }

  private async Task WakeDevice(DeviceResponseDto device)
  {
    if (device.MacAddresses is null ||
        device.MacAddresses.Length == 0)
    {
      Snackbar.Add("No MAC addresses on device", Severity.Warning);
      return;
    }

    await ViewerHub.SendWakeDevice(device.MacAddresses);
    Snackbar.Add("Wake command sent", Severity.Success);
  }
}