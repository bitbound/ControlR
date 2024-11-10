using ControlR.Web.Client.Services.Stores;
using Microsoft.AspNetCore.Components;

namespace ControlR.Web.Client.Components;

public partial class Dashboard
{
  private readonly Dictionary<string, SortDefinition<DeviceResponseDto>> _sortDefinitions = new()
  {
    ["IsOnline"] = new SortDefinition<DeviceResponseDto>(nameof(DeviceResponseDto.IsOnline), true, 0, x => x.IsOnline),
    ["Name"] = new SortDefinition<DeviceResponseDto>(nameof(DeviceResponseDto.Name), false, 1, x => x.Name)
  };

  private Version? _agentReleaseVersion;
  private bool _hideOfflineDevices;
  private bool _loading = true;
  private string? _searchText;

  [Inject]
  public required IBusyCounter BusyCounter { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IDeviceStore DeviceStore { get; init; }

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
  public required IViewerHubConnection ViewerHub { get; init; }


  [Inject]
  public required IDeviceContentWindowStore WindowStore { get; init; }

  private ICollection<DeviceResponseDto> FilteredDevices
  {
    get
    {
      if (!_hideOfflineDevices || IsHideOfflineDevicesDisabled)
      {
        return DeviceStore.Items;
      }

      return DeviceStore.Items
        .Where(x => x.IsOnline)
        .ToArray();
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
      catch (Exception ex)
      {
        Logger.LogError(ex, "Error while filtering devices.");
      }
    }

    return false;
  };

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();

    using var _ = BusyCounter.IncrementBusyCounter();

    _hideOfflineDevices = await Settings.GetHideOfflineDevices();

    await RefreshLatestAgentVersion();

    Messenger.RegisterEventMessage(this, HandleEventMessage);

    if (DeviceStore.Items.Count == 0)
    {
      await DeviceStore.Refresh();
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

      var dialogOptions = new DialogOptions
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
      var dialogRef =
        await DialogService.ShowAsync<AppSettingsEditorDialog>("Agent App Settings", parameters, dialogOptions);
      var result = await dialogRef.Result;
      if (result?.Data is true)
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


  private Task HandleEventMessage(object subscriber, EventMessageKind kind)
  {
    try
    {
      switch (kind)
      {
        case EventMessageKind.DeviceStoreUpdated:
        {
          Debouncer.Debounce(
            TimeSpan.FromSeconds(1),
            async () => { await InvokeAsync(StateHasChanged); });
        }
          break;
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while handling event message kind {MessageKind}.", kind);
    }

    return Task.CompletedTask;
  }

  private async Task HandleRefreshClicked()
  {
    Snackbar.Add("Refreshing devices", Severity.Success);
    await RefreshDevices();
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

  private async Task RefreshDevices()
  {
    try
    {
      _loading = true;
      using var _ = BusyCounter.IncrementBusyCounter();
      await InvokeAsync(StateHasChanged);
      await RefreshLatestAgentVersion();
      await DeviceStore.Refresh();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while refreshing the dashboard.");
      Snackbar.Add("Dashboard refresh failed", Severity.Error);
    }
    finally
    {
      _loading = false;
      await InvokeAsync(StateHasChanged);
    }
  }

  private async Task RefreshLatestAgentVersion()
  {
    var agentVerResult = await ControlrApi.GetCurrentAgentVersion();
    if (agentVerResult.IsSuccess)
    {
      _agentReleaseVersion = agentVerResult.Value;
    }
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

        var dialogParams = new DialogParameters { ["DeviceName"] = device.Name, ["Sessions"] = sessionResult.Value };
        var dialogRef =
          await DialogService.ShowAsync<WindowsSessionSelectDialog>("Select Target Session", dialogParams);
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
            new Dictionary<string, object?>
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

  private async Task RemoveDevice(DeviceResponseDto device)
  {
    try
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

      var deleteResult = await ControlrApi.DeleteDevice(device.Id);
      if (!deleteResult.IsSuccess)
      {
        Snackbar.Add(deleteResult.Reason, Severity.Error);
        return;
      }

      _ = DeviceStore.Remove(device);
      Snackbar.Add("Device removed", Severity.Success);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while removing device.");
    }
  }

  private async Task RestartDevice(DeviceResponseDto device)
  {
    try
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
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while restarting device.");
    }
  }

  private async Task ShutdownDevice(DeviceResponseDto device)
  {
    try
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
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while shutting down device.");
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
        new Dictionary<string, object?>
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

  private async Task UninstallAgent(DeviceResponseDto device)
  {
    try
    {
      var result = await DialogService.ShowMessageBox(
        "Confirm Uninstall",
        $"Are you sure you want to uninstall the agent from {device.Name}?",
        "Yes",
        "No");

      if (result != true)
      {
        return;
      }

      await ViewerHub.UninstallAgent(device.Id, "Manually uninstalled.");
      Snackbar.Add("Uninstall command sent", Severity.Success);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while shutting down device.");
    }
  }

  private async Task UpdateDevice(DeviceResponseDto device)
  {
    try
    {
      Snackbar.Add("Sending update request", Severity.Success);
      await ViewerHub.SendAgentUpdateTrigger(device);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending update request.");
    }
  }

  private async Task WakeDevice(DeviceResponseDto device)
  {
    try
    {
      if (device.MacAddresses.Length == 0)
      {
        Snackbar.Add("No MAC addresses on device", Severity.Warning);
        return;
      }

      await ViewerHub.SendWakeDevice(device.MacAddresses);
      Snackbar.Add("Wake command sent", Severity.Success);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending wake command.");
    }
  }
}