using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.ObjectModel;
using System.Runtime.Versioning;

namespace ControlR.Web.Client.Components;

[SupportedOSPlatform("browser")]
public partial class Dashboard
{
  private readonly Dictionary<string, SortDefinition<DeviceViewModel>> _sortDefinitions = new()
  {
    ["IsOnline"] = new SortDefinition<DeviceViewModel>(nameof(DeviceViewModel.IsOnline), true, 0, x => x.IsOnline),
    ["Name"] = new SortDefinition<DeviceViewModel>(nameof(DeviceViewModel.Name), false, 1, x => x.Name)
  };

  private Version? _agentReleaseVersion;
  private ObservableCollection<DeviceViewModel> _devices = [];  
  private bool _hideOfflineDevices;
  private bool _loading = true;
  private string? _searchText;
  private HashSet<TagViewModel> _selectedTags = [];

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
  public required ITagStore TagStore { get; init; }
  
  [Inject]
  public required IViewerHubConnection ViewerHub { get; init; }

  [Inject]
  public required IDeviceContentWindowStore WindowStore { get; init; }


  private bool ShouldBypassHideOfflineDevices =>
    !string.IsNullOrWhiteSpace(_searchText);

  private ICollection<DeviceViewModel> FilteredDevices
  {
    get
    {
      var devices = _devices.AsEnumerable();
      
      // Filter by online status if enabled
      if (_hideOfflineDevices && !ShouldBypassHideOfflineDevices)
      {
        devices = devices.Where(x => x.IsOnline);
      }

      // Filter by selected tags if any are selected
      if (_selectedTags.Count > 0)
      {
        devices = devices.Where(device =>
          _selectedTags.Any(tag => tag.DeviceIds.Contains(device.Id)));
      }

      return [.. devices];
    }
  }

  private Func<DeviceViewModel, bool> QuickFilter => x =>
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

    using var token = BusyCounter.IncrementBusyCounter();

    _hideOfflineDevices = await Settings.GetHideOfflineDevices();

    Messenger.Register<HubConnectionStateChangedMessage>(this, HandleHubConnectionStateChangedMessage);
    Messenger.Register<DtoReceivedMessage<DeviceDto>>(this, HandleDeviceDtoReceived);

    if (DeviceStore.Items.Count == 0)
    {
      await DeviceStore.Refresh();
    }

    if (TagStore.Items.Count == 0)
    {
      await TagStore.Refresh();
    }

    _selectedTags = [.. TagStore.Items];

    await LoadDevicesFromStore();

    _loading = false;
  }

  private async Task EditDevice(DeviceViewModel device)
  {
    try
    {
      // TODO: Implement EditDevice.
      await Task.Yield();
      //var settingsResult = await ControlrApi.GetDeviceDetails(device.Id);
      //if (!settingsResult.IsSuccess)
      //{
      //  Snackbar.Add(settingsResult.Reason, Severity.Error);
      //  return;
      //}

      //var dialogOptions = new DialogOptions
      //{
      //  BackdropClick = false,
      //  FullWidth = true,
      //  MaxWidth = MaxWidth.Medium
      //};

      //var parameters = new DialogParameters
      //{
      //  { nameof(AppSettingsEditorDialog.AppSettings), settingsResult.Value },
      //  { nameof(AppSettingsEditorDialog.DeviceViewModel), device }
      //};
      //var dialogRef =
      //  await DialogService.ShowAsync<AppSettingsEditorDialog>("Agent App Settings", parameters, dialogOptions);
      //var result = await dialogRef.Result;
      //if (result?.Data is true)
      //{
      //  Snackbar.Add("Settings saved on device", Severity.Success);
      //}
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while getting device settings.");
      Snackbar.Add("Failed to get device settings", Severity.Error);
    }
  }

  private string GetTagsMultiSelectText(List<string> tags)
  {
    if (tags.Count == 0)
    {
      return "No tags selected";
    }

    if (_selectedTags.Count == TagStore.Items.Count)
    {
      return "All tags selected";
    }

    var tagNoun = tags.Count > 1 ? "tags" : "tag";
    return $"{tags.Count} {tagNoun} selected";
  }

  private async Task HandleDeviceDtoReceived(object subscriber, DtoReceivedMessage<DeviceDto> message)
  {
    var viewModel = message.Dto.CloneAs<DeviceDto, DeviceViewModel>();
    viewModel.IsOutdated = IsOutdated(viewModel);

    var index = _devices.FindIndex(x => x.Id == viewModel.Id);
    if (index > -1)
    {
      _devices[index] = viewModel;
    }
    else
    {
      _devices.Add(viewModel);
    }
    await InvokeAsync(StateHasChanged);
  }

  private async Task HandleHubConnectionStateChangedMessage(object subscriber, HubConnectionStateChangedMessage message)
  {
    if (message.NewState == HubConnectionState.Connected)
    {
      await RefreshDevices();
    }
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

  private bool IsOutdated(DeviceViewModel device)
  {
    return
      _agentReleaseVersion is not null &&
      Version.TryParse(device.AgentVersion, out var agentVersion) &&
      !agentVersion.Equals(_agentReleaseVersion);
  }

  private async Task LoadDevicesFromStore()
  {
    var agentVerResult = await ControlrApi.GetCurrentAgentVersion();
    if (agentVerResult.IsSuccess)
    {
      _agentReleaseVersion = agentVerResult.Value;
    }

    var devices = DeviceStore.Items.CloneAs<ICollection<DeviceDto>, ObservableCollection<DeviceViewModel>>();
    foreach (var device in devices)
    {
      device.IsOutdated = IsOutdated(device);
    }

    _devices = devices;
  }

  private Task OnSelectedTagsChanged(IEnumerable<TagViewModel> tags)
  {
    _selectedTags = [.. tags];
    return InvokeAsync(StateHasChanged);
  }
  private async Task RefreshDeviceInfo(DeviceViewModel device)
  {
    try
    {
      await ViewerHub.RefreshDeviceInfo(device.Id);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while refreshing device info.");
      Snackbar.Add("An error occurred while refreshing device info", Severity.Error);
    }
  }

  private async Task RefreshDevices()
  {
    try
    {
      _loading = true;
      using var _ = BusyCounter.IncrementBusyCounter();
      await InvokeAsync(StateHasChanged);
      await DeviceStore.Refresh();
      await LoadDevicesFromStore();
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


  private async Task RemoteControlClicked(DeviceViewModel device)
  {
    switch (device.Platform)
    {
      case SystemPlatform.Windows:
        var sessionResult = await ViewerHub.GetWindowsSessions(device.Id);
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

  private async Task RemoveDevice(DeviceViewModel device)
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

      _devices.Remove(device);
      if (DeviceStore.TryGet(device.Id, out var dto))
      {
        _ = DeviceStore.Remove(dto);
      }
      Snackbar.Add("Device removed", Severity.Success);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while removing device.");
    }
  }

  private async Task RestartDevice(DeviceViewModel device)
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

      await ViewerHub.SendPowerStateChange(device.Id, PowerStateChangeType.Restart);
      Snackbar.Add("Restart command sent", Severity.Success);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while restarting device.");
    }
  }

  private async Task ShutdownDevice(DeviceViewModel device)
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

      await ViewerHub.SendPowerStateChange(device.Id, PowerStateChangeType.Shutdown);
      Snackbar.Add("Shutdown command sent", Severity.Success);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while shutting down device.");
    }
  }
  private void StartTerminal(DeviceViewModel device)
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

  private async Task UninstallAgent(DeviceViewModel device)
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

  private async Task UpdateDevice(Guid deviceId)
  {
    try
    {
      Snackbar.Add("Sending update request", Severity.Success);
      await ViewerHub.SendAgentUpdateTrigger(deviceId);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending update request.");
    }
  }

  private async Task WakeDevice(DeviceViewModel device)
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
