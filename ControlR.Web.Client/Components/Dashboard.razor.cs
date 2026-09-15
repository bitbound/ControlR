using ControlR.Libraries.Api.Contracts.FilterSort;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Customers;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceGroups;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Immutable;
using System.Runtime.Versioning;

namespace ControlR.Web.Client.Components;

[SupportedOSPlatform("browser")]
public partial class Dashboard : IAsyncDisposable
{
  // Must not exceed the server's per-call subscription cap (ViewerHub.MaxHeartbeatSubscriptionBatch).
  private const int SubscriptionBatchSize = 100;

  private readonly ManualResetEventAsync _componentLoadedSignal = new(false);
  private readonly DisposableCollection _disposables = [];
  private readonly SemaphoreSlim _heartbeatSyncLock = new(1, 1);
  private readonly Dictionary<string, SortDefinition<DeviceViewModel>> _sortDefinitions = new()
  {
    ["IsOnline"] = new SortDefinition<DeviceViewModel>(nameof(DeviceViewModel.Dto.IsOnline), true, 0, x => x.Dto.IsOnline),
    ["Name"] = new SortDefinition<DeviceViewModel>(nameof(DeviceViewModel.Dto.Name), false, 1, x => x.Dto.Name)
  };

  private bool? _anyDevicesForUser;
  private List<CustomerDto> _customers = [];
  private MudDataGrid<DeviceViewModel>? _dataGrid;
  private FilterMatchMode _deviceGroupFilterMatchMode = FilterMatchMode.Any;
  private List<DeviceGroupDto> _deviceGroups = [];
  private InternalDtos.DeviceSearchFilterCountsDto _filterCounts = new();
  private bool _hideOfflineDevices;
  private bool _loading = true;
  private bool _openDeviceInNewTab;
  private int _rowsPerPage = 25;
  private string? _searchText;
  private HashSet<Guid> _selectedCustomerIds = [];
  private HashSet<Guid> _selectedDeviceGroupIds = [];
  private ImmutableArray<TagViewModel> _selectedTags = [];
  private bool _showOnlyUngrouped;
  private bool _showOnlyUntagged;
  private HashSet<Guid> _subscribedDeviceIds = [];
  private FilterMatchMode _tagFilterMatchMode = FilterMatchMode.Any;
  private Guid _tenantId;
  private int _totalFilteredDevices;

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

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
  public required IPersistentStateAccessor ServerSettings { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required ITagStore TagStore { get; init; }

  [Inject]
  public required IUserPreferencesProvider UserPreferences { get; init; }

  [Inject]
  public required IDeviceContentWindowStore WindowStore { get; init; }

  private bool ShouldBypassHideOfflineDevices =>
    !string.IsNullOrWhiteSpace(_searchText);

  public async ValueTask DisposeAsync()
  {
    await _heartbeatSyncLock.WaitAsync();
    try
    {
      if (MainHub.IsConnected && _subscribedDeviceIds.Count > 0)
      {
        await MainHub.Server.UnsubscribeFromDeviceHeartbeats2(new([.. _subscribedDeviceIds]));
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error unsubscribing from device heartbeats during disposal.");
    }
    finally
    {
      _heartbeatSyncLock.Release();
    }

    _disposables.Dispose();
    _heartbeatSyncLock.Dispose();
    GC.SuppressFinalize(this);
  }

  protected override async Task OnInitializedAsync()
  {
    try
    {
      await base.OnInitializedAsync();

      var preferences = await UserPreferences.GetPreferences();
      _hideOfflineDevices = preferences.HideOfflineDevices;
      _openDeviceInNewTab = preferences.OpenDeviceInNewTab;
      _showOnlyUntagged = preferences.ShowOnlyUntaggedDevices;
      _showOnlyUngrouped = preferences.ShowOnlyUngroupedDevices;

      if (await AuthState.GetTenantId(Snackbar) is not { } tenantId)
      {
        return;
      }

      _tenantId = tenantId;

      if (TagStore.Items.Count == 0)
      {
        await TagStore.Refresh();
      }

      var customersResult = await ControlrApi.V1.Customers.GetAllCustomers(_tenantId);
      if (customersResult.IsSuccess)
      {
        _customers = [.. customersResult.Value.Items];
      }

      var deviceGroupsResult = await ControlrApi.V1.DeviceGroups.GetAllDeviceGroups(_tenantId);
      if (deviceGroupsResult.IsSuccess)
      {
        _deviceGroups = [.. deviceGroupsResult.Value.Items];
      }

      _disposables.AddRange(
        Messenger.Register<HubConnectionStateChangedMessage>(this, HandleHubConnectionStateChangedMessage),
        Messenger.Register<DtoReceivedMessage<InternalDtos.DeviceResponseDto>>(this, HandleDeviceDtoReceived)
      );


      _loading = false;
      _componentLoadedSignal.Set();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error during dashboard initialization.");
      Snackbar.Add("An error occurred during dashboard initialization.", Severity.Error);
    }
  }

  private string GetCustomerMultiSelectText(IReadOnlyList<string> customers)
  {
    if (customers.Count == 0)
    {
      return string.Empty;
    }
    var tagNoun = customers.Count > 1 ? "customers" : "customer";
    return $"{customers.Count} {tagNoun} selected";
  }

  private string GetCustomerSelectText()
  {
    if (_selectedCustomerIds.Count == 0)
    {
      return string.Empty;
    }
    var tagNoun = _selectedCustomerIds.Count > 1 ? "customers" : "customer";
    return $"{_selectedCustomerIds.Count} {tagNoun} selected";
  }

  private string GetDeviceGroupMultiSelectText(IReadOnlyList<string> deviceGroups)
  {
    if (deviceGroups.Count == 0)
    {
      return string.Empty;
    }
    var groupNoun = deviceGroups.Count > 1 ? "groups" : "group";
    return $"{deviceGroups.Count} {groupNoun} selected";
  }

  private string GetDeviceGroupSelectText()
  {
    if (_selectedDeviceGroupIds.Count == 0)
    {
      return string.Empty;
    }
    var groupNoun = _selectedDeviceGroupIds.Count > 1 ? "groups" : "group";
    return $"{_selectedDeviceGroupIds.Count} {groupNoun} selected";
  }

  private async Task HandleDeviceDtoReceived(object subscriber, DtoReceivedMessage<InternalDtos.DeviceResponseDto> message)
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
      // Server-side group memberships reset on (re)connect; re-subscribe during the refresh.
      _subscribedDeviceIds = [];
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
    await UserPreferences.SetPreference(UserPreferenceNames.HideOfflineDevices, isChecked);
    await ReloadGridData();
  }

  private async Task LaunchDeviceAccess(DeviceViewModel device)
  {
    var uri = $"{NavMan.BaseUri.TrimEnd('/')}/device-access?deviceId={device.Id}";
    if (_openDeviceInNewTab)
    {
      await JsInterop.OpenWindow(uri, "_blank");
    }
    else
    {
      var navOptions = new NavigationOptions()
      {
        ForceLoad = false,
        HistoryEntryState = HistoryEntryStates.CreateDeviceAccess()
      };
      NavMan.NavigateTo($"/device-access?deviceId={device.Id}", navOptions);
    }
  }

  private async Task LaunchRemoteControl(DeviceViewModel device)
  {
    var uri = $"{NavMan.BaseUri.TrimEnd('/')}/device-access/remote-control?deviceId={device.Id}";
    if (_openDeviceInNewTab)
    {
      await JsInterop.OpenWindow(uri, "_blank");
    }
    else
    {
      var navOptions = new NavigationOptions()
      {
        ForceLoad = false,
        HistoryEntryState = HistoryEntryStates.CreateDeviceAccess()
      };
      NavMan.NavigateTo($"/device-access/remote-control?deviceId={device.Id}", navOptions);
    }
  }

  private async Task<GridData<DeviceViewModel>> LoadServerData(GridState<DeviceViewModel> state, CancellationToken cancellationToken)
  {
    if (_loading)
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await _componentLoadedSignal.Wait(cts.Token);
    }

    var tagIds = _showOnlyUntagged ? null : _selectedTags.Select(t => t.Id).ToList();
    IReadOnlyList<Guid>? groupIds = _showOnlyUngrouped
      ? null
      : _selectedDeviceGroupIds.Count > 0 ? [.. _selectedDeviceGroupIds] : null;

    var request = new InternalDtos.DeviceSearchRequestDto
    {
      SearchText = _searchText,
      HideOfflineDevices = _hideOfflineDevices && !ShouldBypassHideOfflineDevices,
      ShowOnlyUntaggedDevices = _showOnlyUntagged,
      ShowOnlyUngroupedDevices = _showOnlyUngrouped,
      TagIds = tagIds,
      CustomerIds = _selectedCustomerIds.Count > 0 ? [.. _selectedCustomerIds] : null,
      DeviceGroupIds = groupIds,
      DeviceGroupFilterMatchMode = _deviceGroupFilterMatchMode,
      TagFilterMatchMode = _tagFilterMatchMode,
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

    var result = await ControlrApi.Internal.Devices.SearchDevices(request, cancellationToken);
    if (!result.IsSuccess)
    {
      _filterCounts = new InternalDtos.DeviceSearchFilterCountsDto();
      _totalFilteredDevices = 0;
      await InvokeAsync(StateHasChanged);
      Snackbar.Add("Failed to load devices", Severity.Error);
      await SyncHeartbeatSubscriptions([]);
      return new GridData<DeviceViewModel> { TotalItems = 0, Items = [] };
    }

    _anyDevicesForUser = result.Value.AnyDevicesForUser;
    _filterCounts = result.Value.FilterCounts;
    _totalFilteredDevices = result.Value.TotalItems;
    await InvokeAsync(StateHasChanged);

    if (result.Value.Items is null)
    {
      await SyncHeartbeatSubscriptions([]);
      return new GridData<DeviceViewModel> { TotalItems = 0, Items = [] };
    }

    var viewModels = result.Value.Items
        .Select(dto =>
        {
          var viewModel = new DeviceViewModel(dto);
          return viewModel;
        })
        .ToArray();

    await SyncHeartbeatSubscriptions(viewModels.Select(viewModel => viewModel.Id));

    return new GridData<DeviceViewModel>
    {
      TotalItems = result.Value.TotalItems,
      Items = viewModels ?? []
    };
  }

  private async Task OnDeviceGroupFilterMatchModeChanged(FilterMatchMode mode)
  {
    _deviceGroupFilterMatchMode = mode;
    await ReloadGridData();
  }

  private async Task OnSearch(string text)
  {
    _searchText = text;
    await ReloadGridData();
  }

  private async Task OnSelectedCustomersChanged(IEnumerable<Guid> customerIds)
  {
    _selectedCustomerIds = [.. customerIds];
    await ReloadGridData();
  }

  private async Task OnSelectedDeviceGroupsChanged(IEnumerable<Guid> deviceGroupIds)
  {
    _selectedDeviceGroupIds = [.. deviceGroupIds];
    if (_showOnlyUngrouped && _selectedDeviceGroupIds.Count > 0)
    {
      _showOnlyUngrouped = false;
      await UserPreferences.SetPreference(UserPreferenceNames.ShowOnlyUngroupedDevices, false);
    }
    await ReloadGridData();
  }

  private async Task OnSelectedTagsChanged(ImmutableArray<TagViewModel> tags)
  {
    _selectedTags = [.. tags];
    if (_showOnlyUntagged && _selectedTags.Length > 0)
    {
      _showOnlyUntagged = false;
      await UserPreferences.SetPreference(UserPreferenceNames.ShowOnlyUntaggedDevices, false);
    }
    await ReloadGridData();
  }

  private async Task OnTagFilterMatchModeChanged(FilterMatchMode mode)
  {
    _tagFilterMatchMode = mode;
    await ReloadGridData();
  }

  private async Task OpenDeviceInNewTabChanged(bool isChecked)
  {
    _openDeviceInNewTab = isChecked;
    await UserPreferences.SetPreference(UserPreferenceNames.OpenDeviceInNewTab, isChecked);
  }

  private async Task RefreshDeviceInfo(DeviceViewModel device)
  {
    try
    {
      var refreshResult = await MainHub.Server.RefreshDeviceInfo2(new(device.Id));
      if (!refreshResult.IsSuccess)
      {
        Snackbar.Add($"Failed to refresh device info: {refreshResult.Reason}", Severity.Warning);
      }
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
      Debouncer.Debounce(
        wait: TimeSpan.FromMilliseconds(500),
        action: async () => await InvokeAsync(_dataGrid.ReloadServerData)
      );
    }
  }

  private async Task RemoveDevice(DeviceViewModel device)
  {
    try
    {
      var result = await DialogService.ShowMessageBoxAsync(
        "Confirm Removal",
        "Are you sure you want to remove this device?",
        "Remove",
        "Cancel");

      if (result != true)
      {
        return;
      }

      var deleteResult = await ControlrApi.Internal.Devices.DeleteDevice(device.Id);
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
      var result = await DialogService.ShowMessageBoxAsync(
        "Confirm Restart",
        $"Are you sure you want to restart {device.Dto.Name}?",
        "Yes",
        "No");

      if (result != true)
      {
        return;
      }

      var restartResult = await MainHub.Server.SendPowerStateChange2(new(device.Id, PowerStateChangeType.Restart));
      if (restartResult.IsSuccess)
      {
        Snackbar.Add("Restart command sent", Severity.Success);
      }
      else
      {
        Snackbar.Add($"Failed to restart device: {restartResult.Reason}", Severity.Error);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while restarting device.");
    }
  }

  private async Task ShowOnlyUngroupedChanged(bool isChecked)
  {
    _showOnlyUngrouped = isChecked;
    await UserPreferences.SetPreference(UserPreferenceNames.ShowOnlyUngroupedDevices, isChecked);
    if (isChecked)
    {
      _selectedDeviceGroupIds = [];
    }
    await ReloadGridData();
  }

  private async Task ShowOnlyUntaggedChanged(bool isChecked)
  {
    _showOnlyUntagged = isChecked;
    await UserPreferences.SetPreference(UserPreferenceNames.ShowOnlyUntaggedDevices, isChecked);
    if (isChecked)
    {
      _selectedTags = [];
    }
    await ReloadGridData();
  }

  private async Task ShutdownDevice(DeviceViewModel device)
  {
    try
    {
      var result = await DialogService.ShowMessageBoxAsync(
        "Confirm Shutdown",
        $"Are you sure you want to shut down {device.Dto.Name}?",
        "Yes",
        "No");

      if (result != true)
      {
        return;
      }

      var shutdownResult = await MainHub.Server.SendPowerStateChange2(new(device.Id, PowerStateChangeType.Shutdown));
      if (shutdownResult.IsSuccess)
      {
        Snackbar.Add("Shutdown command sent", Severity.Success);
      }
      else
      {
        Snackbar.Add($"Failed to shut down device: {shutdownResult.Reason}", Severity.Error);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while shutting down device.");
    }
  }

  private async Task SyncHeartbeatSubscriptions(IEnumerable<Guid> visibleDeviceIds)
  {
    await _heartbeatSyncLock.WaitAsync();
    try
    {
      if (!MainHub.IsConnected)
      {
        // Subscriptions are (re)established when the hub connects and triggers a grid refresh.
        return;
      }

      var visible = visibleDeviceIds.ToHashSet();
      var toSubscribe = visible.Except(_subscribedDeviceIds).ToArray();
      var toUnsubscribe = _subscribedDeviceIds.Except(visible).ToArray();
      var subscribed = new HashSet<Guid>(_subscribedDeviceIds);

      foreach (var batch in toSubscribe.Chunk(SubscriptionBatchSize))
      {
        var result = await MainHub.Server.SubscribeToDeviceHeartbeats2(new(batch));
        if (result.IsSuccess)
        {
          subscribed.UnionWith(batch);
        }
        else
        {
          Logger.LogWarning("Failed to subscribe to device heartbeats: {Reason}", result.Reason);
          Snackbar.Add($"Failed to subscribe to device heartbeats: {result.Reason}", Severity.Warning);
        }
      }

      if (toUnsubscribe.Length > 0)
      {
        await MainHub.Server.UnsubscribeFromDeviceHeartbeats2(new(toUnsubscribe));
        subscribed.ExceptWith(toUnsubscribe);
      }

      _subscribedDeviceIds = subscribed;
    }
    finally
    {
      _heartbeatSyncLock.Release();
    }
  }

  private async Task UninstallAgent(DeviceViewModel device)
  {
    try
    {
      var result = await DialogService.ShowMessageBoxAsync(
        "Confirm Uninstall",
        $"Are you sure you want to uninstall the agent from {device.Dto.Name}?",
        "Yes",
        "No");

      if (result != true)
      {
        return;
      }

      var uninstallResult = await MainHub.Server.UninstallAgent2(new(device.Id, "Manually uninstalled."));
      if (uninstallResult.IsSuccess)
      {
        Snackbar.Add("Uninstall command sent", Severity.Success);
      }
      else
      {
        Snackbar.Add($"Failed to uninstall agent: {uninstallResult.Reason}", Severity.Error);
      }
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
      var updateResult = await MainHub.Server.SendAgentUpdateTrigger2(new(deviceId));
      if (updateResult.IsSuccess)
      {
        Snackbar.Add("Sending update request", Severity.Success);
      }
      else
      {
        Snackbar.Add($"Failed to send update request: {updateResult.Reason}", Severity.Error);
      }
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
      if (device.Dto.MacAddresses.Count == 0)
      {
        Snackbar.Add("No MAC addresses on device", Severity.Warning);
        return;
      }

      var result = await MainHub.Server.SendWakeDevice2(new(device.Id, device.Dto.MacAddresses.ToArray()));
      if (result.IsSuccess)
      {
        Snackbar.Add(result.Value, Severity.Success);
      }
      else
      {
        Snackbar.Add(result.Reason, Severity.Error);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending wake command.");
    }
  }
}
