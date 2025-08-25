using System.Web;
using ControlR.Web.Client.Extensions;
using ControlR.Web.Client.Services.DeviceAccess;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;

namespace ControlR.Web.Client.Components.Layout.DeviceAccess;

public partial class DeviceAccessLayout
{
  private const string DeviceIdSessionStorageKey = "controlr-device-id";
  private MudTheme? _customTheme;
  private Guid _deviceId;
  private string? _deviceName;
  private bool _drawerOpen = true;
  private string? _errorText;
  private HubConnectionState _hubConnectionState = HubConnectionState.Disconnected;
  private string? _loadingText = "Loading";
  private bool _isAuthenticated;

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IDeviceAccessState DeviceAccessState { get; init; }

  [Inject]
  public required ILogger<DeviceAccessLayout> Logger { get; init; }

  [Inject]
  public required IMessenger Messenger { get; init; }

  [Inject]
  public required NavigationManager NavManager { get; init; }

  [Inject]
  public required ISessionStorageAccessor SessionStorageAccessor { get; init; }

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required IViewerHubConnection ViewerHub { get; init; }

  private MudTheme CustomTheme =>
    _customTheme ??= new MudTheme
    {
      PaletteDark = Theme.DarkPalette
    };

  private bool IsNavMenuDisabled
  {
    get
    {
      if (!RendererInfo.IsInteractive)
      {
        return true;
      }

      if (_hubConnectionState != HubConnectionState.Connected)
      {
        return true;
      }

      if (DeviceAccessState.IsDeviceLoaded != true)
      {
        return true;
      }

      return !DeviceAccessState.CurrentDevice.IsOnline;
    }
  }

  protected override async Task OnInitializedAsync()
  {
    try
    {
      await base.OnInitializedAsync();
      _loadingText = "Loading";
      _isAuthenticated = await AuthState.IsAuthenticated();

      if (!_isAuthenticated)
      {
        _errorText = "Authorization is required.";
        return;
      }

      if (RendererInfo.IsInteractive)
      {
        _loadingText = "Connecting";
        await InvokeAsync(StateHasChanged);

        Messenger.Register<ToastMessage>(this, HandleToastMessage);
        Messenger.Register<DtoReceivedMessage<DeviceDto>>(this, HandleDeviceDtoReceivedMessage);
        Messenger.Register<HubConnectionStateChangedMessage>(this, HandleHubConnectionStateChanged);
        await ViewerHub.Connect();
        await GetDeviceInfo();
      }
    }
    catch (Exception ex)
    {
      _errorText = "An error occurred during initialization.";
      Logger.LogError(ex, "Error initializing DeviceAccessLayout");
    }
    finally
    {
      _loadingText = null;
      await InvokeAsync(StateHasChanged);
    }
  }

  private async Task GetDeviceInfo()
  {
    var currentUri = new Uri(NavManager.Uri);
    var query = HttpUtility.ParseQueryString(currentUri.Query);
    var deviceIdString = query.Get("deviceId");

    if (string.IsNullOrWhiteSpace(deviceIdString))
    {
      deviceIdString = await SessionStorageAccessor.GetItem(DeviceIdSessionStorageKey);

      if (string.IsNullOrWhiteSpace(deviceIdString))
      {
        _errorText = "A valid Device ID was not provided.";
        return;
      }
    }

    if (!Guid.TryParse(deviceIdString, out _deviceId) || _deviceId == Guid.Empty)
    {
      _errorText = "A valid Device ID was not provided.";
      return;
    }

    await SessionStorageAccessor.SetItem(DeviceIdSessionStorageKey, $"{_deviceId}");

    try
    {
      var result = await ControlrApi.GetDevice(_deviceId);
      if (!result.IsSuccess || result.Value is null)
      {
        _errorText = "Device not found.";
        return;
      }

      DeviceAccessState.CurrentDevice = result.Value;
      _deviceName = result.Value.Name;
      _errorText = null;
      if (!result.Value.IsOnline)
      {
        NavManager.NavigateTo("/device-access", false);
      }
    }
    catch (Exception ex)
    {
      _errorText = "Failed to fetch device details.";
      Logger.LogError(ex, "Error fetching device details for {DeviceId}", _deviceId);
      return;
    }
  }

  private async Task HandleDeviceDtoReceivedMessage(object subscriber, DtoReceivedMessage<DeviceDto> message)
  {
    if (DeviceAccessState.CurrentDevice is null)
    {
      return;
    }

    if (message.Dto.Id == DeviceAccessState.CurrentDevice.Id)
    {
      DeviceAccessState.CurrentDevice = message.Dto;
      _deviceName = message.Dto.Name;
      if (!message.Dto.IsOnline)
      {
        Snackbar.Add("Agent went offline", Severity.Warning);
        NavManager.NavigateTo("/device-access", false);
      }

      await InvokeAsync(StateHasChanged);
    }
  }

  private async Task HandleHubConnectionStateChanged(object subscriber, HubConnectionStateChangedMessage message)
  {
    _hubConnectionState = message.NewState;
    await InvokeAsync(StateHasChanged);
  }

  private Task HandleToastMessage(object subscriber, ToastMessage toast)
  {
    Snackbar.Add(toast.Message, toast.Severity);
    return Task.CompletedTask;
  }

  private void ToggleNavDrawer()
  {
    _drawerOpen = !_drawerOpen;
  }
}