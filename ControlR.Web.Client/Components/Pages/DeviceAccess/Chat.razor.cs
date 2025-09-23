using ControlR.Web.Client.Services.DeviceAccess;
using ControlR.Web.Client.Services.DeviceAccess.Chat;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ControlR.Web.Client.Components.Pages.DeviceAccess;

public partial class Chat : ComponentBase, IDisposable
{
  private readonly string _chatInputElementId = $"chat-input-{Guid.NewGuid()}";
  private string? _alertMessage;
  private Severity _alertSeverity;
  private MudTextField<string> _chatInputElement = null!;
  private ElementReference _chatMessagesContainer;
  private string? _loadingMessage = "Loading";
  private DeviceUiSession[]? _systemSessions;
  private IDisposable? _stateChangeHandler;

  [Inject]
  public required IChatState ChatState { get; init; }

  [Inject]
  public required IDeviceState DeviceAccessState { get; init; }

  [Inject]
  public required IJsInterop JsInterop { get; init; }

  [Inject]
  public required ILogger<Chat> Logger { get; init; }

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

  private int ChatInputLineCount => ChatState.EnableMultiline
    ? 6
    : 1;

  private string ChatInputHelperText => ChatState.EnableMultiline
    ? "Type a message and press Ctrl+Enter to send, or Enter for new line"
    : "Type a message and press Enter to send";

  private ChatPageState CurrentState
  {
    get
    {
      if (!string.IsNullOrWhiteSpace(_loadingMessage))
      {
        return ChatPageState.Loading;
      }

      if (!string.IsNullOrWhiteSpace(_alertMessage))
      {
        return ChatPageState.Alert;
      }

      if (DeviceAccessState.CurrentDevice.Platform
          is not SystemPlatform.Windows
          and not SystemPlatform.MacOs
          and not SystemPlatform.Linux)
      {
        return ChatPageState.UnsupportedOperatingSystem;
      }

      if (ChatState.CurrentSession is not null)
      {
        return ChatPageState.ChatActive;
      }

      return _systemSessions is not null
        ? ChatPageState.SessionSelect
        : ChatPageState.Unknown;
    }
  }

  private string OuterDivClass
  {
    get
    {
      if (CurrentState == ChatPageState.ChatActive)
      {
        return "h-100";
      }

      return "h-100 ma-4";
    }
  }


  public void Dispose()
  {
    _stateChangeHandler?.Dispose();
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

    _stateChangeHandler = ChatState.OnStateChanged(HandleChatStateChanged);

    await LoadSystemSessions();
  }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    await base.OnAfterRenderAsync(firstRender);
    // If this is the first render and we have existing messages, scroll to end after a short delay
    // to ensure the DOM is fully rendered with content.
    if (firstRender && ChatState.ChatMessages.Count > 0)
    {
      await Task.Delay(50);
      await JsInterop.ScrollToEnd(_chatMessagesContainer);
    }
  }

  private async Task CloseChatSession()
  {
    if (ChatState.CurrentSession is not null)
    {
      var result = await ViewerHub.CloseChatSession(
        DeviceAccessState.CurrentDevice.Id,
        ChatState.SessionId,
        ChatState.CurrentSession.ProcessId);

      if (!result.IsSuccess)
      {
        Logger.LogError("Failed to close chat session: {Error}", result.Exception?.Message);
        Snackbar.Add("Failed to close chat session", Severity.Warning);
      }
    }

    ChatState.Clear();
    await InvokeAsync(StateHasChanged);
  }

  private async Task HandleChatStateChanged()
  {
    // Update the UI
    await InvokeAsync(StateHasChanged);
    await JsInterop.ScrollToEnd(_chatMessagesContainer);
  }


  private async Task HandleInputKeyDown(KeyboardEventArgs args)
  {
    if (args.Key == "Enter" && !args.ShiftKey)
    {
      if (ChatState.EnableMultiline && args.CtrlKey)
      {
        await SendMessage();
      }
      else if (!ChatState.EnableMultiline)
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
        _alertMessage = $"Failed to get active sessions: {sessionResult.Reason}.";
        _alertSeverity = Severity.Warning;
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

  private async Task Reload()
  {
    _alertMessage = null;
    _loadingMessage = null;
    await LoadSystemSessions();
  }

  private async Task SendMessage()
  {
    if (string.IsNullOrWhiteSpace(ChatState.NewMessage) || ChatState.CurrentSession is null)
    {
      return;
    }

    try
    {
      var chatDto = new ChatMessageHubDto(
        DeviceAccessState.CurrentDevice.Id,
        ChatState.SessionId,
        ChatState.NewMessage.Trim(),
        string.Empty, // SenderName will be set in the hub
        string.Empty, // SenderEmail will be set in the hub
        ChatState.CurrentSession.SystemSessionId,
        ChatState.CurrentSession.ProcessId,
        DateTimeOffset.Now);

      // Add the message to our local chat
      var chatMessage = new ChatMessage
      {
        Message = ChatState.NewMessage.Trim(),
        SenderName = "You",
        Timestamp = DateTimeOffset.Now,
        IsFromViewer = true
      };
      ChatState.ChatMessages.Add(chatMessage);

      // Clear the input
      ChatState.NewMessage = string.Empty;

      // Send to the device
      var result = await ViewerHub.SendChatMessage(DeviceAccessState.CurrentDevice.Id, chatDto);
      if (!result.IsSuccess)
      {
        Logger.LogError("Failed to send chat message: {Error}", result.Exception?.Message);
        Snackbar.Add("Failed to send message", Severity.Error);
      }

      await InvokeAsync(StateHasChanged);
      // Focus back to input
      await _chatInputElement.FocusAsync();
      await JsInterop.ScrollToEnd(_chatMessagesContainer);
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
      ChatState.Clear();
      ChatState.CurrentSession = session;
      ChatState.SessionId = Guid.NewGuid();

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

public enum ChatPageState
{
  Unknown,
  Loading,
  Alert,
  SessionSelect,
  ChatActive,
  UnsupportedOperatingSystem
}