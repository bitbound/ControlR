using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
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

  private readonly ManualResetEventAsync _componentLoadedSignal = new(false);
  private Version? _agentReleaseVersion;
  private bool? _anyDevicesForUser;
  private MudDataGrid<DeviceViewModel>? _dataGrid;
  private bool _hideOfflineDevices;
  private bool _loading = true;
  private string? _searchText;
  private HashSet<TagViewModel> _selectedTags = [];

  [Inject]
  public required IBusyCounter BusyCounter { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

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

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();

    using var token = BusyCounter.IncrementBusyCounter();

    _hideOfflineDevices = await Settings.GetHideOfflineDevices();
    await SetLatestAgentVersion();

    Messenger.Register<HubConnectionStateChangedMessage>(this, HandleHubConnectionStateChangedMessage);
    Messenger.Register<DtoReceivedMessage<DeviceDto>>(this, HandleDeviceDtoReceived);

    _loading = false;
    _componentLoadedSignal.Set();
  }

  private async Task HandleDeviceDtoReceived(object subscriber, DtoReceivedMessage<DeviceDto> message)
  {
    var viewModel = message.Dto.CloneAs<DeviceDto, DeviceViewModel>();
    if (_dataGrid?.FilteredItems.Any(x => x.Id == viewModel.Id) == true)
    {
      await ReloadGridData();
    }
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
    await ReloadGridData();
  }

  private bool IsOutdated(DeviceViewModel device)
  {
    return
      _agentReleaseVersion is not null &&
      Version.TryParse(device.AgentVersion, out var agentVersion) &&
      !agentVersion.Equals(_agentReleaseVersion);
  }

  private async Task<GridData<DeviceViewModel>> LoadServerData(GridState<DeviceViewModel> state)
  {
    if (_loading)
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await _componentLoadedSignal.Wait(cts.Token);
    }

    var tagIds = _selectedTags.Count > 0 && _selectedTags.Count < TagStore.Items.Count
        ? _selectedTags.Select(t => t.Id).ToList()
        : null;

    var request = new DeviceSearchRequestDto
    {
      SearchText = _searchText,
      HideOfflineDevices = _hideOfflineDevices && !ShouldBypassHideOfflineDevices,
      TagIds = tagIds,
      Page = state.Page,
      PageSize = state.PageSize,
      SortDefinitions = [.. state.SortDefinitions
          .Select(sd => new DeviceColumnSort
          {
              PropertyName = sd.SortBy,
              Descending = sd.Descending,
              SortOrder = sd.Index
          })],
      FilterDefinitions = [.. state.FilterDefinitions
          .Select(fd => new DeviceColumnFilter
          {
              PropertyName = fd.Column?.PropertyName,
              Operator = fd.Operator,
              Value = fd.Value?.ToString()
          })]
    };

    var result = await ControlrApi.SearchDevices(request);
    if (!result.IsSuccess)
    {
      Snackbar.Add("Failed to load devices", Severity.Error);
      return new GridData<DeviceViewModel> { TotalItems = 0, Items = [] };
    }

    _anyDevicesForUser = result.Value.AnyDevicesForUser;

    if (result.Value.Items is null)
    {
      return new GridData<DeviceViewModel> { TotalItems = 0, Items = [] };
    }

    var viewModels = result.Value.Items
        .Select(dto =>
        {
          var viewModel = dto.CloneAs<DeviceDto, DeviceViewModel>();
          viewModel.IsOutdated = IsOutdated(viewModel);
          return viewModel;
        })
        .ToArray();

    return new GridData<DeviceViewModel>
    {
      TotalItems = result.Value.TotalItems,
      Items = viewModels ?? []
    };
  }

  private async Task OnSearch(string text)
  {
    _searchText = text;
    await ReloadGridData();
  }

  private async Task OnSelectedTagsChanged(ImmutableArray<TagViewModel> tags)
  {
    _selectedTags = [.. tags];
    await ReloadGridData();
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
      await SetLatestAgentVersion();
      await InvokeAsync(StateHasChanged);
      await ReloadGridData();
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

  private async Task ReloadGridData()
  {
    if (_dataGrid is not null)
    {
      await _dataGrid.ReloadServerData();
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

  private Task VncConnectClicked(DeviceViewModel device)
  {
    WindowStore.AddContentInstance<VncFrame>(
      device,
      DeviceContentInstanceType.VncFrame,
      new Dictionary<string, object?>
      {
        [nameof(VncFrame.Device)] = device
      });
    return Task.CompletedTask;
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

      Snackbar.Add("Device removed", Severity.Success);
      await ReloadGridData();
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
  private async Task SetLatestAgentVersion()
  {
    var agentVerResult = await ControlrApi.GetCurrentAgentVersion();
    if (agentVerResult.IsSuccess)
    {
      _agentReleaseVersion = agentVerResult.Value;
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
