using System.Web;
using ControlR.Web.Client.Services.DeviceAccess;
using Microsoft.AspNetCore.Components;
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

  [Inject]
  public required ILazyDi<IControlrApi> ControlrApi { get; init; }

  [Inject]
  public required ILazyDi<IDeviceAccessState> DeviceAccessState { get; init; }

  [Inject]
  public required ILogger<DeviceAccessLayout> Logger { get; init; }

  [Inject]
  public required ILazyDi<IMessenger> Messenger { get; init; }

  [Inject]
  public required ILazyDi<NavigationManager> NavManager { get; init; }

  [Inject]
  public required ILazyDi<ISessionStorageAccessor> SessionStorageAccessor { get; init; }

  [Inject]
  public required ILazyDi<ISnackbar> Snackbar { get; init; }

  [Inject]
  public required ILazyDi<IViewerHubConnection> ViewerHub { get; init; }

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

      if (DeviceAccessState.Maybe?.IsDeviceLoaded != true)
      {
        return true;
      }

      return !DeviceAccessState.Value.CurrentDevice.IsOnline;
    }
  }

  protected override async Task OnInitializedAsync()
  {
    try
    {
      _loadingText = "Loading";
      await base.OnInitializedAsync();

      if (RendererInfo.IsInteractive)
      {
        _loadingText = "Connecting";
        await InvokeAsync(StateHasChanged);

        Messenger.Value.Register<ToastMessage>(this, HandleToastMessage);
        Messenger.Value.Register<DtoReceivedMessage<DeviceDto>>(this, HandleDeviceDtoReceivedMessage);
        Messenger.Value.Register<HubConnectionStateChangedMessage>(this, HandleHubConnectionStateChanged);
        await ViewerHub.Value.Connect();
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
    var sessionStorage = SessionStorageAccessor.Value;
    var currentUri = new Uri(NavManager.Value.Uri);
    var query = HttpUtility.ParseQueryString(currentUri.Query);
    var deviceIdString = query.Get("deviceId");

    if (string.IsNullOrWhiteSpace(deviceIdString))
    {
      deviceIdString = await sessionStorage.GetItem(DeviceIdSessionStorageKey);

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

    await sessionStorage.SetItem(DeviceIdSessionStorageKey, $"{_deviceId}");

    try
    {
      var result = await ControlrApi.Value.GetDevice(_deviceId);
      if (!result.IsSuccess || result.Value is null)
      {
        _errorText = "Device not found.";
        return;
      }

      DeviceAccessState.Value.CurrentDevice = result.Value;
      _deviceName = result.Value.Name;
      _errorText = null;
      if (!result.Value.IsOnline)
      {
        NavManager.Value.NavigateTo("/device-access", false);
      }
    }
    catch (Exception ex)
    {
      _errorText = "Failed to fetch device details.";
      Logger.LogError(ex, "Error fetching device details for {DeviceId}", _deviceId);
      return;
    }

    NavManager.Value.NavigateTo(currentUri.AbsolutePath, false);
  }

  private async Task HandleDeviceDtoReceivedMessage(object subscriber, DtoReceivedMessage<DeviceDto> message)
  {
    if (DeviceAccessState.Maybe?.CurrentDevice is null)
    {
      return;
    }

    if (message.Dto.Id == DeviceAccessState.Value.CurrentDevice.Id)
    {
      DeviceAccessState.Value.CurrentDevice = message.Dto;
      _deviceName = message.Dto.Name;
      if (!message.Dto.IsOnline)
      {
        Snackbar.Value.Add("Agent went offline", Severity.Warning);
        NavManager.Value.NavigateTo("/device-access", false);
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
    Snackbar.Maybe?.Add(toast.Message, toast.Severity);
    return Task.CompletedTask;
  }

  private void ToggleNavDrawer()
  {
    _drawerOpen = !_drawerOpen;
  }
}