using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Web.Client.Services.DeviceAccess;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace ControlR.Web.Client.Components.Pages.DeviceAccess;

public partial class Chat : ComponentBase, IDisposable
{
  private string? _alertMessage;
  private Severity _alertSeverity;
  private string? _loadingMessage = "Loading";
  private string _newMessage = string.Empty;
  private MudTextField<string> _messageInput = null!;

  private DeviceUiSession[]? _systemSessions;
  private DeviceUiSession? _selectedSession;
  private Guid _currentChatSessionId = Guid.NewGuid();
  private List<ChatMessage> _chatMessages = [];

  [Inject]
  public required IDeviceAccessState DeviceAccessState { get; init; }

  [Inject]
  public required ILogger<Chat> Logger { get; init; }

  [Inject]
  public required IMessenger Messenger { get; init; }

  [Inject]
  public required NavigationManager NavManager { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required IViewerHubConnection ViewerHub { get; init; }

  private string AlertIcon =>
    _alertSeverity switch
    {
      Severity.Normal or Severity.Info => Icons.Material.Outlined.Info,
      Severity.Success => Icons.Material.Outlined.CheckCircleOutline,
      Severity.Warning => Icons.Material.Outlined.Warning,
      Severity.Error => Icons.Material.Outlined.Error,
      _ => Icons.Material.Outlined.Info
    };

  private ChatState CurrentState
  {
    get
    {
      if (!string.IsNullOrWhiteSpace(_loadingMessage))
      {
        return ChatState.Loading;
      }

      if (!string.IsNullOrWhiteSpace(_alertMessage))
      {
        return ChatState.Alert;
      }

      if (DeviceAccessState.CurrentDevice.Platform 
        is not SystemPlatform.Windows 
        and not SystemPlatform.MacOs
        and not SystemPlatform.Linux)
      {
        return ChatState.UnsupportedOperatingSystem;
      }

      if (_selectedSession is not null)
      {
        return ChatState.ChatActive;
      }

      return _systemSessions is not null
        ? ChatState.SessionSelect
        : ChatState.Unknown;
    }
  }

  private string OuterDivClass
  {
    get
    {
      if (CurrentState == ChatState.ChatActive)
      {
        return "h-100";
      }

      return "h-100 ma-4";
    }
  }

  protected override async Task OnInitializedAsync()
  {
    if (DeviceAccessState.CurrentDevice.Id == Guid.Empty)
    {
      _alertMessage = "No device selected. Please go back and select a device.";
      _alertSeverity = Severity.Warning;
      return;
    }

    // Register for incoming chat responses
    Messenger.Register<DtoReceivedMessage<ChatResponseHubDto>>(this, HandleChatResponseReceived);

    await LoadSystemSessions();
  }

  protected override void OnAfterRender(bool firstRender)
  {
    if (firstRender)
    {
      // Any additional setup after first render
    }
  }

  public void Dispose()
  {
    Messenger.UnregisterAll(this);
  }

  private async Task LoadSystemSessions()
  {
    try
    {
      _loadingMessage = "Loading system sessions...";
      StateHasChanged();

      var result = await ViewerHub.GetActiveUiSessions(DeviceAccessState.CurrentDevice.Id);
      if (result.IsSuccess)
      {
        _systemSessions = result.Value;
        _loadingMessage = null;
        
        if (_systemSessions.Length == 0)
        {
          _alertMessage = "No active system sessions found on the target device.";
          _alertSeverity = Severity.Warning;
        }
      }
      else
      {
        _alertMessage = "Failed to load system sessions. Please try again.";
        _alertSeverity = Severity.Error;
        _loadingMessage = null;
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error loading system sessions.");
      _alertMessage = "An error occurred while loading system sessions.";
      _alertSeverity = Severity.Error;
      _loadingMessage = null;
    }
    finally
    {
      StateHasChanged();
    }
  }

  private async Task RefreshSystemSessions()
  {
    await LoadSystemSessions();
  }

  private async Task StartChatSession(DeviceUiSession session)
  {
    try
    {
      _selectedSession = session;
      _currentChatSessionId = Guid.NewGuid();
      _chatMessages.Clear();
      
      Logger.LogInformation(
        "Starting chat session with {Username} on session {SessionId}, process {ProcessId}",
        session.Username,
        session.SystemSessionId,
        session.ProcessId);

      _loadingMessage = null;
      _alertMessage = null;
      StateHasChanged();

      // Focus the message input
      await Task.Delay(100);
      await _messageInput.FocusAsync();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error starting chat session.");
      _alertMessage = "Failed to start chat session.";
      _alertSeverity = Severity.Error;
      StateHasChanged();
    }
  }

  private void CloseChatSession()
  {
    _selectedSession = null;
    _chatMessages.Clear();
    _currentChatSessionId = Guid.Empty;
    StateHasChanged();
  }

  private async Task SendMessage()
  {
    if (string.IsNullOrWhiteSpace(_newMessage) || _selectedSession is null)
    {
      return;
    }

    try
    {
      // Get current user info (this would need to be available from a service)
      var userDisplayName = "Current User"; // TODO: Get from user service
      var userEmail = "user@example.com"; // TODO: Get from user service

      var chatDto = new ChatMessageHubDto(
        DeviceAccessState.CurrentDevice.Id,
        _currentChatSessionId,
        _newMessage.Trim(),
        userDisplayName,
        userEmail,
        _selectedSession.SystemSessionId,
        _selectedSession.ProcessId,
        DateTimeOffset.Now);

      // Add the message to our local chat
      var chatMessage = new ChatMessage
      {
        Message = _newMessage.Trim(),
        SenderName = userDisplayName,
        Timestamp = DateTimeOffset.Now,
        IsFromViewer = true
      };
      _chatMessages.Add(chatMessage);

      // Clear the input
      _newMessage = string.Empty;
      StateHasChanged();

      // Send to the device
      var result = await ViewerHub.SendChatMessage(DeviceAccessState.CurrentDevice.Id, chatDto);
      if (!result.IsSuccess)
      {
        Logger.LogError("Failed to send chat message: {Error}", result.Exception?.Message);
        Snackbar.Add("Failed to send message", Severity.Error);
      }

      // Focus back to input
      await _messageInput.FocusAsync();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error sending chat message.");
      Snackbar.Add("Error sending message", Severity.Error);
    }
  }

  private async Task HandleKeyDown(KeyboardEventArgs args)
  {
    if (args.Key == "Enter" && !args.ShiftKey)
    {
      await SendMessage();
    }
  }

  private async Task HandleChatResponseReceived(object subscriber, DtoReceivedMessage<ChatResponseHubDto> message)
  {
    try
    {
      var response = message.Dto;
      
      // Only handle responses for the current chat session
      if (response.SessionId != _currentChatSessionId)
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

      _chatMessages.Add(chatMessage);
      
      // Update the UI
      await InvokeAsync(StateHasChanged);
      
      Logger.LogInformation(
        "Received chat response from {Username} for session {SessionId}",
        response.SenderUsername,
        response.SessionId);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error handling received chat response");
    }
  }

  // TODO: Handle incoming chat responses from desktop client
  // This would need to be implemented when the reverse communication is set up
}

public enum ChatState
{
  Unknown,
  Loading,
  Alert,
  SessionSelect,
  ChatActive,
  UnsupportedOperatingSystem
}

public class ChatMessage
{
  public string Message { get; set; } = string.Empty;
  public string SenderName { get; set; } = string.Empty;
  public DateTimeOffset Timestamp { get; set; }
  public bool IsFromViewer { get; set; }
}
