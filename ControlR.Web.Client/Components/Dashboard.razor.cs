using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Immutable;
using System.Runtime.Versioning;

namespace ControlR.Web.Client.Components;

[SupportedOSPlatform("browser")]
public partial class Dashboard
{
  private readonly ManualResetEventAsync _componentLoadedSignal = new(false);
  private readonly Dictionary<string, SortDefinition<DeviceViewModel>> _sortDefinitions = new()
  {
    ["IsOnline"] = new SortDefinition<DeviceViewModel>(nameof(DeviceViewModel.Dto.IsOnline), true, 0, x => x.Dto.IsOnline),
    ["Name"] = new SortDefinition<DeviceViewModel>(nameof(DeviceViewModel.Dto.Name), false, 1, x => x.Dto.Name)
  };

  private bool? _anyDevicesForUser;
  private MudDataGrid<DeviceViewModel>? _dataGrid;
  private bool _hideOfflineDevices;
  private bool _loading = true;
  private int _rowsPerPage = 25;
  private string? _searchText;
  private HashSet<TagViewModel> _selectedTags = [];

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IDialogService DialogService { get; init; }

  [Inject]
  public required IJsInterop JsInterop { get; init; }

  [Inject]
  public required ILogger<Dashboard> Logger { get; init; }

  [Inject]
  public required IHubConnection<IViewerHub> MainHub { get; init; }

  [Inject]
  public required IMessenger Messenger { get; init; }

  [Inject]
  public required NavigationManager NavMan { get; init; }

  [Inject]
  public required IUserSettingsProvider Settings { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required IUserTagStore UserTagStore { get; init; }

  [Inject]
  public required IDeviceContentWindowStore WindowStore { get; init; }

  private bool ShouldBypassHideOfflineDevices =>
    !string.IsNullOrWhiteSpace(_searchText);

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();

    _hideOfflineDevices = await Settings.GetHideOfflineDevices();

    Messenger.Register<HubConnectionStateChangedMessage>(this, HandleHubConnectionStateChangedMessage);
    Messenger.Register<DtoReceivedMessage<DeviceResponseDto>>(this, HandleDeviceDtoReceived);

    _loading = false;
    _componentLoadedSignal.Set();
  }

  private async Task HandleDeviceDtoReceived(object subscriber, DtoReceivedMessage<DeviceResponseDto> message)
  {
    var viewModel = new DeviceViewModel(message.Dto);
    if (_dataGrid?.FilteredItems.Any(x => x.Id == viewModel.Id) == true ||
        _dataGrid?.FilteredItems.Count() < _rowsPerPage)
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

  private async Task LaunchDeviceAccess(DeviceViewModel device)
  {
    var uri = $"{NavMan.BaseUri.TrimEnd('/')}/device-access?deviceId={device.Id}";
    await JsInterop.OpenWindow(uri, "_blank");
  }

  private async Task LaunchRemoteControl(DeviceViewModel device)
  {
    var uri = $"{NavMan.BaseUri.TrimEnd('/')}/device-access/remote-control?deviceId={device.Id}";
    await JsInterop.OpenWindow(uri, "_blank");
  }

  private async Task<GridData<DeviceViewModel>> LoadServerData(GridState<DeviceViewModel> state)
  {
    if (_loading)
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await _componentLoadedSignal.Wait(cts.Token);
    }

    var tagIds = _selectedTags.Count > 0 && _selectedTags.Count < UserTagStore.Items.Count
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
          var viewModel = new DeviceViewModel(dto);
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
      await MainHub.Server.RefreshDeviceInfo(device.Id);
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
      await InvokeAsync(_dataGrid.ReloadServerData);
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
        $"Are you sure you want to restart {device.Dto.Name}?",
        "Yes",
        "No");

      if (result != true)
      {
        return;
      }

      await MainHub.Server.SendPowerStateChange(device.Id, PowerStateChangeType.Restart);
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
        $"Are you sure you want to shut down {device.Dto.Name}?",
        "Yes",
        "No");

      if (result != true)
      {
        return;
      }

      await MainHub.Server.SendPowerStateChange(device.Id, PowerStateChangeType.Shutdown);
      Snackbar.Add("Shutdown command sent", Severity.Success);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while shutting down device.");
    }
  }
  private async Task UninstallAgent(DeviceViewModel device)
  {
    try
    {
      var result = await DialogService.ShowMessageBox(
        "Confirm Uninstall",
        $"Are you sure you want to uninstall the agent from {device.Dto.Name}?",
        "Yes",
        "No");

      if (result != true)
      {
        return;
      }

      await MainHub.Server.UninstallAgent(device.Id, "Manually uninstalled.");
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
      await MainHub.Server.SendAgentUpdateTrigger(deviceId);
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
      if (device.Dto.MacAddresses.Length == 0)
      {
        Snackbar.Add("No MAC addresses on device", Severity.Warning);
        return;
      }

      await MainHub.Server.SendWakeDevice(device.Id, device.Dto.MacAddresses);
      Snackbar.Add("Wake command sent", Severity.Success);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending wake command.");
    }
  }
}
