using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Avalonia.Controls.Snackbar;

namespace ControlR.Viewer.Avalonia.ViewModels;

public interface IChatViewModel : IViewModelBase
{
  string? AlertMessage { get; set; }
  SnackbarSeverity AlertSeverity { get; }
  ObservableCollection<ChatMessage> ChatMessages { get; }
  string ChatTitle { get; }
  IAsyncRelayCommand CloseChatCommand { get; }
  int CommandInputHeight { get; }
  ChatPageState CurrentState { get; }
  ObservableCollection<IChatDesktopCardViewModel> DesktopSessions { get; }
  string DesktopSessionTitle { get; }
  bool EnableMultiline { get; set; }
  bool HasDesktopSessions { get; }
  string? LoadingMessage { get; set; }
  string NewMessage { get; set; }
  IAsyncRelayCommand RefreshSessionsCommand { get; }
  IAsyncRelayCommand ReloadCommand { get; }
  IAsyncRelayCommand SendMessageCommand { get; }
  Task StartChat(DesktopSession session);
}

public partial class ChatViewModel : ViewModelBase<ChatView>, IChatViewModel
{
  private readonly IChatState _chatState;
  private readonly IDeviceState _deviceState;
  private readonly ILogger<ChatViewModel> _logger;
  private readonly ISnackbar _snackbar;
  private readonly IHubConnection<IViewerHub> _viewerHub;
  private readonly IOptions<ControlrViewerOptions> _viewerOptions;

  private IDisposable? _stateChangeHandler;

  public ChatViewModel(
    IHubConnection<IViewerHub> viewerHub,
    IDeviceState deviceState,
    IChatState chatState,
    IOptions<ControlrViewerOptions> viewerOptions,
    ISnackbar snackbar,
    ILogger<ChatViewModel> logger)
  {
    _viewerHub = viewerHub;
    _deviceState = deviceState;
    _chatState = chatState;
    _viewerOptions = viewerOptions;
    _snackbar = snackbar;
    _logger = logger;
  }

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(CurrentState))]
  public partial string? AlertMessage { get; set; }
  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(CurrentState))]
  public partial SnackbarSeverity AlertSeverity { get; set; } = SnackbarSeverity.Info;
  public ObservableCollection<ChatMessage> ChatMessages {get; } = [];
  public string ChatTitle => _chatState.CurrentSession is not null
    ? string.Format(
        CultureInfo.CurrentCulture,
        Resources.Chat_ChatWith,
        _chatState.CurrentSession.Username,
        _chatState.CurrentSession.Name)
    : string.Empty;
  public int CommandInputHeight => EnableMultiline ? 120 : 40;
  public ChatPageState CurrentState
  {
    get
    {
      if (!string.IsNullOrWhiteSpace(LoadingMessage))
      {
        return ChatPageState.Loading;
      }

      if (!string.IsNullOrWhiteSpace(AlertMessage))
      {
        return ChatPageState.Alert;
      }

      if (_deviceState.TryGetCurrentDevice()?.Platform
          is not SystemPlatform.Windows
          and not SystemPlatform.MacOs
          and not SystemPlatform.Linux)
      {
        return ChatPageState.UnsupportedOperatingSystem;
      }

      if (_chatState.CurrentSession is not null)
      {
        return ChatPageState.ChatActive;
      }

      return ChatPageState.SessionSelect;
    }
  }
  public ObservableCollection<IChatDesktopCardViewModel> DesktopSessions { get; } = [];
  public string DesktopSessionTitle
  {
    get
    {
      var deviceName = _deviceState.TryGetCurrentDevice()?.Name;
      if (string.IsNullOrWhiteSpace(deviceName))
      {
        deviceName = Resources.ConnectionStatus_Unknown;
      }

      return string.Format(
        CultureInfo.CurrentCulture,
        Resources.DesktopSessionsOnDevice,
        deviceName);
    }
  }
  public bool EnableMultiline
  {
    get => _chatState.EnableMultiline;
    set
    {
      if (_chatState.EnableMultiline == value)
      {
        return;
      }

      _chatState.EnableMultiline = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CommandInputHeight));
    }
  }
  public bool HasDesktopSessions => DesktopSessions.Count > 0;
  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(CurrentState))]
  public partial string? LoadingMessage { get; set; } = Resources.Chat_Loading;
  public string NewMessage
  {
    get => _chatState.NewMessage;
    set
    {
      if (_chatState.NewMessage == value)
      {
        return;
      }

      _chatState.NewMessage = value;
      OnPropertyChanged();
    }
  }

  public async Task StartChat(DesktopSession session)
  {
    try
    {
      _chatState.Clear();
      _chatState.CurrentSession = session;
      _chatState.SessionId = Guid.NewGuid();

      AlertMessage = null;
      AlertSeverity = SnackbarSeverity.Info;
      LoadingMessage = null;

      OnPropertyChanged(nameof(CurrentState));
      OnPropertyChanged(nameof(ChatTitle));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to start chat session.");
      _snackbar.Add(Resources.Chat_FailedToStartChat, SnackbarSeverity.Warning);
    }
  }

  protected override void Dispose(bool disposing)
  {
    if (disposing)
    {
      _stateChangeHandler?.Dispose();
    }

    base.Dispose(disposing);
  }

  protected override async ValueTask DisposeAsync(bool disposing)
  {
    if (disposing)
    {
      try
      {
        await CloseChat();
      }
      catch (Exception ex)
      {
        _logger?.LogError(ex, "Failed to close chat session during disposal.");
      }
    }

    await base.DisposeAsync(disposing);
  }

  protected override async Task OnInitializeAsync()
  {
    try
    {
      await base.OnInitializeAsync();

      LoadingMessage = Resources.Chat_Loading;
      AlertMessage = null;
      AlertSeverity = SnackbarSeverity.Info;

      _stateChangeHandler?.Dispose();
      _stateChangeHandler = _chatState.OnStateChanged(HandleChatStateChanged);

      await GetDesktopSessionsAsync();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to retrieve desktop sessions for chat.");
      AlertMessage = Resources.Chat_FailedToLoadSessions;
      AlertSeverity = SnackbarSeverity.Warning;
    }
    finally
    {
      LoadingMessage = null;
    }
  }

  [RelayCommand]
  private async Task CloseChat()
  {
    try
    {
      if (_chatState.CurrentSession is not null)
      {
        var result = await _viewerHub.Server.CloseChatSession(
          _viewerOptions.Value.DeviceId,
          _chatState.SessionId,
          _chatState.CurrentSession.ProcessId);

        if (!result.IsSuccess)
        {
          _logger.LogError("Failed to close chat session: {Error}", result.Reason);
          _snackbar.Add(Resources.Chat_FailedToCloseSession, SnackbarSeverity.Warning);
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error closing chat session.");
      _snackbar.Add(Resources.Chat_FailedToCloseSession, SnackbarSeverity.Error);
    }
    finally
    {
      _chatState.Clear();
      OnPropertyChanged(nameof(CurrentState));
      OnPropertyChanged(nameof(ChatTitle));
    }
  }

  private async Task GetDesktopSessionsAsync()
  {
    try
    {
      var desktopSessions = await _viewerHub.Server.GetActiveDesktopSessions(_viewerOptions.Value.DeviceId);

      DesktopSessions.Clear();
      foreach (var session in desktopSessions)
      {
        DesktopSessions.Add(new ChatDesktopCardViewModel(session, StartChat));
      }

      OnPropertyChanged(nameof(HasDesktopSessions));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to get active desktop sessions for chat.");
      throw;
    }
  }

  private async Task HandleChatStateChanged()
  {
    if (_chatState.ChatMessages.Count != ChatMessages.Count)
    {
      var messages = _chatState.ChatMessages.ToList();
      ChatMessages.Clear();
      ChatMessages.AddRange(messages);
      OnPropertyChanged(nameof(ChatMessages));
    }

    OnPropertyChanged(nameof(CurrentState));
    OnPropertyChanged(nameof(ChatTitle));
  }

  [RelayCommand]
  private async Task RefreshSessions()
  {
    try
    {
      AlertMessage = null;
      AlertSeverity = SnackbarSeverity.Info;
      await GetDesktopSessionsAsync();
      _snackbar.Add(Resources.RemoteControl_SessionsRefreshed, SnackbarSeverity.Info);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to refresh desktop sessions for chat.");
      _snackbar.Add(Resources.Chat_FailedToLoadSessions, SnackbarSeverity.Warning);
    }
  }

  [RelayCommand]
  private async Task Reload()
  {
    AlertMessage = null;
    AlertSeverity = SnackbarSeverity.Info;
    LoadingMessage = Resources.Chat_Loading;
    try
    {
      await GetDesktopSessionsAsync();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to reload chat sessions.");
      AlertMessage = Resources.Chat_FailedToLoadSessions;
      AlertSeverity = SnackbarSeverity.Warning;
    }
    finally
    {
      LoadingMessage = null;
    }
  }

  [RelayCommand]
  private async Task SendMessage()
  {
    try
    {
      var message = NewMessage?.Trim();
      if (string.IsNullOrWhiteSpace(message) || _chatState.CurrentSession is null)
      {
        return;
      }

      var dto = new ChatMessageHubDto(
        _viewerOptions.Value.DeviceId,
        _chatState.SessionId,
        message,
        string.Empty,
        string.Empty,
        _chatState.CurrentSession.SystemSessionId,
        _chatState.CurrentSession.ProcessId,
        DateTimeOffset.Now);

      _chatState.ChatMessages.Add(new ChatMessage
      {
        IsFromViewer = true,
        Message = message,
        SenderName = Resources.Chat_You,
        Timestamp = DateTimeOffset.Now
      });

      await _viewerHub.Server.SendChatMessage(_viewerOptions.Value.DeviceId, dto);

      await _chatState.NotifyStateChanged();
      
      NewMessage = string.Empty;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to send chat message.");
      _snackbar.Add(Resources.Chat_FailedToSend, SnackbarSeverity.Warning);
    }
  }
}
