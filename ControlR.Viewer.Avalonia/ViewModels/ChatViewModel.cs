using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Avalonia.Controls.Snackbar;
using ControlR.Libraries.Avalonia.Services;

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
  private readonly IUiDispatcher _dispatcher;
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
    IUiDispatcher dispatcher,
    ISnackbar snackbar,
    ILogger<ChatViewModel> logger)
  {
    _viewerHub = viewerHub;
    _deviceState = deviceState;
    _chatState = chatState;
    _viewerOptions = viewerOptions;
    _dispatcher = dispatcher;
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

      if (!await GetDesktopSessions())
      {
        AlertMessage = Resources.Chat_FailedToLoadSessions;
        AlertSeverity = SnackbarSeverity.Warning;
      }
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
        var result = await _viewerHub.Server.CloseChatSession2(new(
          _viewerOptions.Value.DeviceId,
          _chatState.SessionId,
          _chatState.CurrentSession.ProcessId));

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

  private async Task<bool> GetDesktopSessions()
  {
    var desktopSessionsResult = await _viewerHub.Server.GetActiveDesktopSessions2(new(_viewerOptions.Value.DeviceId));
    if (!desktopSessionsResult.IsSuccess)
    {
      _logger.LogError("Failed to get active desktop sessions for chat: {Error}", desktopSessionsResult.Reason);
      DesktopSessions.Clear();
      OnPropertyChanged(nameof(HasDesktopSessions));
      return false;
    }

    var desktopSessions = desktopSessionsResult.Value?.ToArray() ?? [];

    DesktopSessions.Clear();
    foreach (var session in desktopSessions)
    {
      DesktopSessions.Add(new ChatDesktopCardViewModel(session, StartChat));
    }

    OnPropertyChanged(nameof(HasDesktopSessions));
    return true;
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
      if (await GetDesktopSessions())
      {
        _snackbar.Add(Resources.RemoteControl_SessionsRefreshed, SnackbarSeverity.Info);
      }
      else
      {
        _snackbar.Add(Resources.Chat_FailedToLoadSessions, SnackbarSeverity.Warning);
      }
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
      if (!await GetDesktopSessions())
      {
        AlertMessage = Resources.Chat_FailedToLoadSessions;
        AlertSeverity = SnackbarSeverity.Warning;
      }
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
    var message = NewMessage?.Trim();
    if (string.IsNullOrWhiteSpace(message) || _chatState.CurrentSession is null)
    {
      return;
    }

    var chatMessage = new ChatMessage
    {
      IsFromViewer = true,
      Message = message,
      SenderName = Resources.Chat_You,
      Timestamp = DateTimeOffset.Now
    };

    try
    {
      _chatState.ChatMessages.Add(chatMessage);
      NewMessage = string.Empty;

      var dto = new ChatMessageHubDto(
        _viewerOptions.Value.DeviceId,
        _chatState.SessionId,
        message,
        string.Empty,
        string.Empty,
        _chatState.CurrentSession.SystemSessionId,
        _chatState.CurrentSession.ProcessId,
        DateTimeOffset.Now);

      var sendResult = await _viewerHub.Server.SendChatMessage2(new(_viewerOptions.Value.DeviceId, dto));
      if (!sendResult.IsSuccess)
      {
        _logger.LogError("Failed to send chat message: {Error}", sendResult.Reason);
        _snackbar.Add(Resources.Chat_FailedToSend, SnackbarSeverity.Warning);
        NewMessage = message;
        _chatState.ChatMessages.Remove(chatMessage);
        return;
      }

      if (_dispatcher.CheckAccess())
      {
        await _chatState.NotifyStateChanged();
      }
      else
      {
        await _dispatcher.InvokeAsync(_chatState.NotifyStateChanged);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to send chat message.");
      _snackbar.Add(Resources.Chat_FailedToSend, SnackbarSeverity.Warning);
      _chatState.ChatMessages.Remove(chatMessage);
      NewMessage = message;
    }
  }
}
