using System.Web;
using ControlR.Web.Client.Extensions;
using ControlR.Web.Client.Services.DeviceAccess;
using ControlR.Web.Client.Services.DeviceAccess.Chat;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;

namespace ControlR.Web.Client.Components.Layout.DeviceAccess;

public partial class DeviceAccessLayout : IAsyncDisposable
{
  private const string DeviceIdSessionStorageKey = "controlr-device-id";
  private MudTheme? _customTheme;
  private Guid _deviceId;
  private string? _deviceName;
  private bool _drawerOpen = true;
  private string? _errorText;
  private HubConnectionState _hubConnectionState = HubConnectionState.Disconnected;
  private bool _isAuthenticated;
  private string? _loadingText = "Loading";

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

  [Inject]
  public required IChatState ChatState { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IHubConnection<IDeviceAccessHub> DeviceAccessHub { get; init; }

  [Inject]
  public required IDeviceState DeviceAccessState { get; init; }

  [Inject]
  public required IHubConnector HubConnector { get; init; }

  [Inject]
  public required ILogger<DeviceAccessLayout> Logger { get; init; }

  [Inject]
  public required IMessenger Messenger { get; init; }

  [Inject]
  public required NavigationManager NavManager { get; init; }

  [Inject]
  public required ISessionStorageAccessor SessionStorageAccessor { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required ITerminalState TerminalState { get; init; }

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

      if (!DeviceAccessState.IsDeviceLoaded)
      {
        return true;
      }

      return !DeviceAccessState.CurrentDevice.IsOnline;
    }
  }

  public async ValueTask DisposeAsync()
  {
    try
    {
      await TryDisposeChat();
      await TryDisposeTerminal();
      ChatState.Clear();
      Messenger.UnregisterAll(this);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error during DeviceAccessLayout disposal.");
    }
    GC.SuppressFinalize(this);
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

      // If authenticated and logonToken/deviceId are present in URL, navigate to clean URL (omit secret token)
      if (RendererInfo.IsInteractive)
      {
        var uri = new Uri(NavManager.Uri);
        if (!string.IsNullOrEmpty(uri.Query) && uri.Query.Contains("logonToken", StringComparison.OrdinalIgnoreCase))
        {
          var basePath = uri.GetLeftPart(UriPartial.Path);
          var query = HttpUtility.ParseQueryString(uri.Query);
          query.Remove("logonToken");
          // Rebuild the remaining query string (retain deviceId and any future params)
          var remaining = query.HasKeys() ? "?" + string.Join('&', query.AllKeys.Select(k => $"{k}={query[k]}")) : string.Empty;
          NavManager.NavigateTo(basePath + remaining, replace: true);
        }
      }

      if (RendererInfo.IsInteractive)
      {
        _loadingText = "Connecting";
        await InvokeAsync(StateHasChanged);

        Messenger.Register<ToastMessage>(this, HandleToastMessage);
        Messenger.Register<DtoReceivedMessage<DeviceDto>>(this, HandleDeviceDtoReceivedMessage);
        Messenger.Register<HubConnectionStateChangedMessage>(this, HandleHubConnectionStateChanged);
        Messenger.Register<DtoReceivedMessage<ChatResponseHubDto>>(this, HandleChatResponseReceived);

        await HubConnector.Connect<IDeviceAccessHub>(AppConstants.DeviceAccessHubPath);
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
    }
  }

  private async Task HandleChatResponseReceived(object subscriber, DtoReceivedMessage<ChatResponseHubDto> message)
  {
    var response = message.Dto;

    // Only handle responses for the current chat session
    if (response.SessionId != ChatState.SessionId)
    {
      return;
    }

    // Add the response to our chat messages
    var chatMessage = new ChatMessage
    {
      Message = response.Message,
      SenderName = response.SenderUsername,
      Timestamp = response.Timestamp,
      IsFromViewer = false
    };

    ChatState.ChatMessages.Add(chatMessage);
    await InvokeAsync(ChatState.NotifyStateChanged);

    if (!NavManager.Uri.Contains("/chat"))
    {
      Snackbar.Add("New chat message received", Severity.Info);
    }
  }

  private async Task HandleDeviceDtoReceivedMessage(object subscriber, DtoReceivedMessage<DeviceDto> message)
  {
    if (DeviceAccessState.CurrentDeviceMaybe is null)
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

  private async Task TryDisposeChat()
  {
    try
    {
      if (!DeviceAccessHub.IsConnected || ChatState.CurrentSession is null)
      {
        return;
      }
      
      await DeviceAccessHub.Server.CloseChatSession(
        DeviceAccessState.CurrentDevice.Id,
        ChatState.SessionId,
        ChatState.CurrentSession.ProcessId);

    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error disposing chat session.");
    }
  }

  private async Task TryDisposeTerminal()
  {
    try
    {
      if (!DeviceAccessHub.IsConnected || TerminalState.Id == Guid.Empty)
      {
        return;
      }
      await DeviceAccessHub.Server.CloseTerminalSession(
        DeviceAccessState.CurrentDevice.Id,
        TerminalState.Id);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error disposing terminal session.");
    }
  }
}