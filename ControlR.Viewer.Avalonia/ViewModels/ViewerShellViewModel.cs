using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.ApiClient;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Avalonia.Controls.Snackbar;
using ControlR.Libraries.Messenger.Extensions.Messages;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Threading;

namespace ControlR.Viewer.Avalonia.ViewModels;

public interface IViewerShellViewModel : IViewModelBase
{
  string? AlertMessage { get; }
  SnackbarSeverity AlertSeverity { get; }
  HubConnectionState ConnectionState { get; }
  string ConnectionStatus { get; }
  IViewModelBase? CurrentViewModel { get; set; }
  bool HasAlertMessage { get; }
  bool IsConnected { get; }
  bool IsDeviceOffline { get; }
  IAsyncRelayCommand ReconnectCommand { get; }
  bool ShowReconnectButton { get; }
}

public partial class ViewerShellViewModel : ViewModelBase<ViewerShell>, IViewerShellViewModel
{
  private readonly IControlrApi _apiClient;
  private readonly IChatState _chatState;
  private readonly IDeviceState _deviceState;
  private readonly IViewerHubConnector _hubConnector;
  private readonly ILogger<ViewerShellViewModel> _logger;
  private readonly IMessenger _messenger;
  private readonly INavigationProvider _navigationProvider;
  private readonly ControlrViewerOptions _options;
  private readonly IServiceProvider _serviceProvider;
  private readonly ISnackbar _snackbar;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(HasAlertMessage))]
  [NotifyPropertyChangedFor(nameof(ShowReconnectButton))]
  private string? _alertMessage;
  [ObservableProperty]
  private SnackbarSeverity _alertSeverity = SnackbarSeverity.Info;
  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(IsConnected))]
  [NotifyPropertyChangedFor(nameof(ConnectionStatus))]
  [NotifyPropertyChangedFor(nameof(ShowReconnectButton))]
  private HubConnectionState _connectionState;
  [ObservableProperty]
  private IViewModelBase? _currentViewModel;
  private bool _isShellInitialized;

  public ViewerShellViewModel(
    IControlrApi apiClient,
    IViewerHubConnector hubConnector,
    IMessenger messenger,
    INavigationProvider navigationProvider,
    IServiceProvider serviceProvider,
    ISnackbar snackbar,
    IChatState chatState,
    IDeviceState deviceState,
    IOptions<ControlrViewerOptions> options,
    ILogger<ViewerShellViewModel> logger)
  {
    _apiClient = apiClient;
    _hubConnector = hubConnector;
    _messenger = messenger;
    _navigationProvider = navigationProvider;
    _serviceProvider = serviceProvider;
    _snackbar = snackbar;
    _deviceState = deviceState;
    _chatState = chatState;
    _options = options.Value;
    _logger = logger;

    _navigationProvider.NavigationOccurred += OnNavigationOccurred;

    Disposables.AddRange(
      new CallbackDisposable(() => _navigationProvider.NavigationOccurred -= OnNavigationOccurred),
      _deviceState.OnStateChanged(HandleDeviceStateChanged),
      _messenger.Register<DtoReceivedMessage<DeviceResponseDto>>(this, HandleDeviceDtoReceivedMessage),
      _messenger.Register<DtoReceivedMessage<ChatResponseHubDto>>(this, HandleChatResponseReceived),
      _messenger.Register<HubConnectionStateChangedMessage>(this, HandleHubConnectionStateChanged)
    );
  }

  public string ConnectionStatus
  {
    get
    {
      return ConnectionState switch
      {
        HubConnectionState.Connected => Resources.Connected,
        HubConnectionState.Connecting => Resources.ConnectionStatus_Connecting,
        HubConnectionState.Reconnecting => Resources.ConnectionStatus_Reconnecting,
        HubConnectionState.Disconnected => Resources.Disconnected,
        _ => Resources.ConnectionStatus_Unknown
      };
    }
  }
  public bool HasAlertMessage => !string.IsNullOrWhiteSpace(AlertMessage);
  public bool IsConnected => ConnectionState == HubConnectionState.Connected;
  public bool IsDeviceOffline => _deviceState.TryGetCurrentDevice()?.IsOnline == false;
  public bool ShowReconnectButton =>
    ConnectionState == HubConnectionState.Disconnected && _isShellInitialized;

  protected override async Task OnInitializeAsync()
  {
    await base.OnInitializeAsync();
    await InitializeViewer();
  }

  private async Task Connect()
  {
    if (!await _hubConnector.Connect())
    {
      _logger.LogWarning("Failed to connect to viewer hub.");
      _snackbar.Add(Resources.ViewerHub_ConnectionFailed, SnackbarSeverity.Error);
      AlertMessage = Resources.ViewerHub_ConnectionFailed;
      AlertSeverity = SnackbarSeverity.Error;
      return;
    }

    AlertMessage = null;

    CurrentViewModel = _serviceProvider.GetRequiredService<IRemoteControlViewModel>();
    await CurrentViewModel.Initialize(forceReinit: true);
  }

  private async Task<bool> GetDeviceInfo()
  {
    var apiResult = await _apiClient.Devices.GetDevice(_options.DeviceId);
    if (apiResult.IsSuccess)
    {
      var device = apiResult.Value;
      _deviceState.CurrentDevice = device;
      return true;
    }

    if (apiResult.StatusCode == HttpStatusCode.Unauthorized)
    {
      AlertMessage = Resources.UnauthorizedAccessPat;
      AlertSeverity = SnackbarSeverity.Error;
      _logger.LogWarning("Unauthorized access when retrieving device info. Check the provided Personal Access Token.");

      _snackbar.Add(Resources.UnauthorizedAccess, SnackbarSeverity.Error);
      return false;
    }

    AlertMessage = string.Format(Resources.DeviceInfo_RetrievalFailedDetails, apiResult.ToString());
    AlertSeverity = SnackbarSeverity.Error;

    _logger.LogWarning("Failed to retrieve device info. Details: {ApiResult}", apiResult);
    _snackbar.Add(Resources.DeviceInfo_RetrievalFailed, SnackbarSeverity.Error);
    return false;
  }

  private async Task HandleChatResponseReceived(object subscriber, DtoReceivedMessage<ChatResponseHubDto> message)
  {
    _logger.LogInformation(
      "Received chat response for session {SessionId} from {Sender}.",
      message.Dto.SessionId,
      message.Dto.SenderUsername);

    var response = message.Dto;

    // Only handle responses for the current chat session.
    if (response.SessionId != _chatState.SessionId)
    {
      return;
    }

    var chatMessage = new ChatMessage
    {
      Message = response.Message,
      SenderName = response.SenderUsername,
      Timestamp = response.Timestamp,
      IsFromViewer = false
    };

    _chatState.ChatMessages.Add(chatMessage);
    if (Dispatcher.UIThread.CheckAccess())
    {
      await _chatState.NotifyStateChanged();
    }
    else
    {
      await Dispatcher.UIThread.InvokeAsync(_chatState.NotifyStateChanged);
    }
  }

  private async Task HandleDeviceDtoReceivedMessage(object subscriber, DtoReceivedMessage<DeviceResponseDto> message)
  {
    try
    {
      if (message.Dto.Id != _options.DeviceId)
      {
        return;
      }

      _deviceState.CurrentDevice = message.Dto;

      if (!message.Dto.IsOnline)
      {
        AlertMessage = Resources.ViewerShell_DeviceOffline;
        AlertSeverity = SnackbarSeverity.Warning;
        _snackbar.Add(Resources.ViewerShell_AgentWentOffline, SnackbarSeverity.Warning);
        return;
      }

      if (AlertMessage == Resources.ViewerShell_DeviceOffline)
      {
        AlertMessage = null;
        AlertSeverity = SnackbarSeverity.Info;
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error handling device DTO received message.");
    }
  }

  private async Task HandleDeviceStateChanged()
  {
    if (Dispatcher.UIThread.CheckAccess())
    {
      OnPropertyChanged(nameof(IsDeviceOffline));
    }
    else
    {
      await Dispatcher.UIThread.InvokeAsync(() => OnPropertyChanged(nameof(IsDeviceOffline)));
    }
  }

  private Task HandleHubConnectionStateChanged(object subscriber, HubConnectionStateChangedMessage message)
  {
    ConnectionState = message.NewState;
    return Task.CompletedTask;
  }

  private async Task InitializeViewer()
  {
    try
    {
      ConnectionState = HubConnectionState.Connecting;
      OnPropertyChanged(nameof(ConnectionStatus));
      AlertMessage = null;
      AlertSeverity = SnackbarSeverity.Info;

      if (!await GetDeviceInfo())
      {
        return;
      }

      await Connect();
    }
    finally
    {
      _isShellInitialized = true;
      OnPropertyChanged(nameof(ConnectionState));
      OnPropertyChanged(nameof(ShowReconnectButton));
    }
  }

  private void OnNavigationOccurred(IViewModelBase? viewModel)
  {
    if (viewModel is null)
    {
      return;
    }

    CurrentViewModel = viewModel;
    viewModel.Initialize();
  }

  [RelayCommand]
  private async Task Reconnect()
  {
    try
    {
      await _hubConnector.Disconnect();
      ConnectionState = HubConnectionState.Disconnected;
      await InitializeViewer();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error reconnecting to viewer hub.");
      _snackbar.Add(Resources.ViewerHub_ReconnectFailed, SnackbarSeverity.Error);
      AlertMessage = Resources.ViewerHub_ReconnectFailed;
      AlertSeverity = SnackbarSeverity.Error;
      return;
    }
  }
}
