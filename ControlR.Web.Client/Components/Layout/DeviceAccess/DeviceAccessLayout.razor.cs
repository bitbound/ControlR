using System.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace ControlR.Web.Client.Components.Layout.DeviceAccess;

public partial class DeviceAccessLayout
{
  private Guid _deviceId;
  private string? _deviceName;
  private string? _errorText;
  private HubConnectionState _hubConnectionState = HubConnectionState.Disconnected;

  [Inject]
  public required ILazyInjector<IChatState> ChatState { get; init; }
  [Inject]
  public required ILazyInjector<IControlrApi> ControlrApi { get; init; }
  [Inject]
  public required ILazyInjector<IDeviceState> DeviceAccessState { get; init; }
  [Inject]
  public required ILazyInjector<IHubConnector> HubConnector { get; init; }
  [Inject]
  public required ILogger<DeviceAccessLayout> Logger { get; init; }
  [Inject]
  public required ILazyInjector<ITerminalState> TerminalState { get; init; }
  [Inject]
  public required ILazyInjector<IHubConnection<IViewerHub>> ViewerHub { get; init; }

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

      if (!DeviceAccessState.Value.IsDeviceLoaded)
      {
        return true;
      }

      return !DeviceAccessState.Value.CurrentDevice.IsOnline;
    }
  }

  public override async ValueTask DisposeAsync()
  {
    try
    {
      if (RendererInfo.IsInteractive)
      {
        await TryDisposeChat();
        await TryDisposeTerminal();
        ChatState.Value.Clear();
      }
      await base.DisposeAsync();
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

      if (!IsAuthenticated)
      {
        _errorText = "Authorization is required.";
        return;
      }

      if (!RendererInfo.IsInteractive)
      {
        // Skip further initialization during prerendering
        return;
      }

      // If authenticated and logonToken/deviceId are present in URL, navigate to clean URL.
      var uri = new Uri(NavManager.Uri);
      if (!string.IsNullOrEmpty(uri.Query) && uri.Query.Contains("logonToken", StringComparison.OrdinalIgnoreCase))
      {
        var basePath = uri.GetLeftPart(UriPartial.Path);
        var query = HttpUtility.ParseQueryString(uri.Query);
        query.Remove("logonToken");
        // Rebuild the remaining query string (retain deviceId and any future params).
        var remaining = query.HasKeys() 
          ? "?" + string.Join('&', query.AllKeys.Select(k => $"{k}={query[k]}")) 
          : string.Empty;

        NavManager.NavigateTo(basePath + remaining, replace: true);
        return;
      }

      await GetDeviceInfo();

      Messenger.Value.Register<DtoReceivedMessage<DeviceDto>>(this, HandleDeviceDtoReceivedMessage);
      Messenger.Value.Register<HubConnectionStateChangedMessage>(this, HandleHubConnectionStateChanged);
      Messenger.Value.Register<DtoReceivedMessage<ChatResponseHubDto>>(this, HandleChatResponseReceived);

      await HubConnector.Value.Connect<IViewerHub>(AppConstants.ViewerHubPath);
    }
    catch (Exception ex)
    {
      _errorText = "An error occurred during initialization.";
      Logger.LogError(ex, "Error initializing DeviceAccessLayout");
    }
  }

  private async Task GetDeviceInfo()
  {
    if (!RendererInfo.IsInteractive)
    {
      return;
    }
    
    var currentUri = new Uri(NavManager.Uri);
    var query = HttpUtility.ParseQueryString(currentUri.Query);
    var deviceIdString = query.Get("deviceId");

    if (string.IsNullOrWhiteSpace(deviceIdString))
    {
      _errorText = "A valid Device ID was not provided.";
      return;
    }

    if (!Guid.TryParse(deviceIdString, out _deviceId) || _deviceId == Guid.Empty)
    {
      _errorText = "A valid Device ID was not provided.";
      return;
    }

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
        NavManager.NavigateTo($"/device-access?deviceId={_deviceId}", false);
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
    if (response.SessionId != ChatState.Value.SessionId)
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

    ChatState.Value.ChatMessages.Add(chatMessage);
    await InvokeAsync(ChatState.Value.NotifyStateChanged);

    if (!NavManager.Uri.Contains("/chat"))
    {
      Snackbar.Value.Add("New chat message received", Severity.Info);
    }
  }
  private async Task HandleDeviceDtoReceivedMessage(object subscriber, DtoReceivedMessage<DeviceDto> message)
  {
    if (DeviceAccessState.Value.TryGetCurrentDevice() is null)
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
        NavManager.NavigateTo($"/device-access?deviceId={_deviceId}", false);
      }

      await InvokeAsync(StateHasChanged);
    }
  }
  private async Task HandleHubConnectionStateChanged(object subscriber, HubConnectionStateChangedMessage message)
  {
    _hubConnectionState = message.NewState;
    await InvokeAsync(StateHasChanged);
  }

  private async Task TryDisposeChat()
  {
    try
    {
      if (!ViewerHub.Value.IsConnected || ChatState.Value.CurrentSession is null)
      {
        return;
      }

      await ViewerHub.Value.Server.CloseChatSession(
        DeviceAccessState.Value.CurrentDevice.Id,
        ChatState.Value.SessionId,
        ChatState.Value.CurrentSession.ProcessId);

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
      if (!ViewerHub.Value.IsConnected || TerminalState.Value.Id == Guid.Empty)
      {
        return;
      }
      await ViewerHub.Value.Server.CloseTerminalSession(
        DeviceAccessState.Value.CurrentDevice.Id,
        TerminalState.Value.Id);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error disposing terminal session.");
    }
  }

}