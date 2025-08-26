using ControlR.Web.Client.Services.DeviceAccess;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ControlR.Web.Client.Components.Pages.DeviceAccess;

public partial class Chat : ComponentBase, IDisposable
{
  private readonly string _chatInputElementId = $"chat-input-{Guid.NewGuid()}";
  private string? _alertMessage;
  private Severity _alertSeverity;
  private MudTextField<string> _chatInputElement = null!;
  private readonly ConcurrentList<ChatMessage> _chatMessages = [];
  private Guid _currentChatSessionId = Guid.NewGuid();
  private bool _enableMultiline;
  private string? _loadingMessage = "Loading";
  private string _newMessage = string.Empty;
  private DeviceUiSession? _selectedSession;
  private DeviceUiSession[]? _systemSessions;

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

  private int ChatInputLineCount => _enableMultiline
    ? 6
    : 1;

  private string ChatInputHelperText => _enableMultiline
    ? "Type a message and press Ctrl+Enter to send, or Enter for new line"
    : "Type a message and press Enter to send";

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
          and not SystemPlatform.MacOs)
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


  public void Dispose()
  {
    Messenger.UnregisterAll(this);
    GC.SuppressFinalize(this);
  }

  protected override async Task OnInitializedAsync()
  {
    if (!DeviceAccessState.IsDeviceLoaded)
    {
      return;
    }

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

  private void CloseChatSession()
  {
    _selectedSession = null;
    _chatMessages.Clear();
    _currentChatSessionId = Guid.Empty;
    StateHasChanged();
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

  private async Task HandleInputKeyDown(KeyboardEventArgs args)
  {
    if (args.Key == "Enter" && !args.ShiftKey)
    {
      if (_enableMultiline && args.CtrlKey)
      {
        await SendMessage();
      }
      else if (!_enableMultiline)
      {
        await SendMessage();
      }
    }
  }

  private async Task LoadSystemSessions()
  {
    try
    {
      var sessionResult = await ViewerHub.GetActiveUiSessions(DeviceAccessState.CurrentDevice.Id);
      if (!sessionResult.IsSuccess)
      {
        Logger.LogResult(sessionResult);
        Snackbar.Add("Failed to get active sessions", Severity.Warning);
        return;
      }

      _systemSessions = sessionResult.Value;
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error loading system sessions.");
      _alertMessage = "An error occurred while loading system sessions.";
      _alertSeverity = Severity.Error;
    }
    finally
    {
      _loadingMessage = null;
      await InvokeAsync(StateHasChanged);
    }
  }

  private async Task RefreshSystemSessions()
  {
    await LoadSystemSessions();
  }

  private async Task SendMessage()
  {
    if (string.IsNullOrWhiteSpace(_newMessage) || _selectedSession is null)
    {
      return;
    }

    try
    {
      var chatDto = new ChatMessageHubDto(
        DeviceAccessState.CurrentDevice.Id,
        _currentChatSessionId,
        _newMessage.Trim(),
        string.Empty, // SenderName will be set in the hub
        string.Empty, // SenderEmail will be set in the hub
        _selectedSession.SystemSessionId,
        _selectedSession.ProcessId,
        DateTimeOffset.Now);

      // Add the message to our local chat
      var chatMessage = new ChatMessage
      {
        Message = _newMessage.Trim(),
        SenderName = "You",
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
      await _chatInputElement.FocusAsync();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error sending chat message.");
      Snackbar.Add("Error sending message", Severity.Error);
    }
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
      await InvokeAsync(StateHasChanged);

      // Focus the message input
      await Task.Delay(100);
      await _chatInputElement.FocusAsync();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error starting chat session.");
      _alertMessage = "Failed to start chat session.";
      _alertSeverity = Severity.Error;
      StateHasChanged();
    }
  }
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
  public bool IsFromViewer { get; set; }
  public string Message { get; set; } = string.Empty;
  public string SenderName { get; set; } = string.Empty;
  public DateTimeOffset Timestamp { get; set; }
}