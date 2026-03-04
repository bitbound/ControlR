using Avalonia.Layout;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.Libraries.Avalonia.Controls.Snackbar;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Messenger.Extensions;
using System.ComponentModel;
using Avalonia.Input.Platform;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Viewer.Avalonia.ViewModels;

public interface IRemoteDisplayViewModel : INotifyPropertyChanged, IDisposable
{
  event EventHandler? FrameQueued;

  CursorChangedDto? ActiveCursor { get; }
  IAsyncRelayCommand DisconnectCommand { get; }
  ObservableCollection<DisplayLayoutItem> DisplayItems { get; }
  bool HasMetricsData { get; }
  bool HasMultipleDisplays { get; }
  bool IsAutoPanEnabled { get; set; }
  bool IsBlockInputToggleEnabled { get; }
  bool IsBlockUserInputEnabled { get; set; }
  bool IsFitViewMode { get; set; }
  bool IsKeyboardInputAuto { get; set; }
  bool IsKeyboardInputPhysical { get; set; }
  bool IsKeyboardInputVirtual { get; set; }
  bool IsMetricsEnabled { get; set; }
  bool IsPrivacyScreenEnabled { get; set; }
  bool IsScaleControlsVisible { get; }
  bool IsScaleViewMode { get; set; }
  bool IsStretchViewMode { get; set; }
  bool IsViewOnlyEnabled { get; set; }
  ILogger<RemoteDisplayViewModel> Logger { get; }
  double MaxRendererScale { get; }
  IReadOnlyDictionary<string, string> MetricsExtraData { get; }
  double MetricsFps { get; }
  TimeSpan MetricsLatency { get; }
  double MetricsMbpsIn { get; }
  double MetricsMbpsOut { get; }
  string MetricsMode { get; }
  double MinRendererScale { get; }
  double RendererHeight { get; }
  HorizontalAlignment RendererHorizontalAlignment { get; }
  double RendererScale { get; set; }
  VerticalAlignment RendererVerticalAlignment { get; }
  double RendererWidth { get; }
  IAsyncRelayCommand<DisplayLayoutItem?> SelectDisplayCommand { get; }
  double SelectedDisplayHeight { get; }
  double SelectedDisplayWidth { get; }
  bool ShowPrivacyScreenToggle { get; }
  bool ShowWindowsInputControls { get; }
  ViewMode ViewMode { get; }

  DisposableValue<SKBitmap?> AcquireCompositedFrame();
  Task InvokeCtrlAltDel();
  Task RequestClipboardText();
  Task SendClipboardText(string text);
  Task SendKeyboardStateReset();
  Task SendKeyEvent(string key, string code, bool isPressed, KeyEventModifiersDto modifiers);
  Task SendMouseButtonEvent(int button, bool isPressed, double percentX, double percentY);
  Task SendPointerMove(double percentX, double percentY);
  Task SendWheelScroll(double percentX, double percentY, double scrollY, double scrollX);
  Task TypeClipboardText(string text);
}

public sealed partial class RemoteDisplayViewModel : ViewModelBase<RemoteDisplayView>, IRemoteDisplayViewModel
{
  private const double DisplayLayoutContainerHeight = 140;
  private const double DisplayLayoutContainerWidth = 280;

  private static readonly IReadOnlyDictionary<string, string> _emptyExtraData = new Dictionary<string, string>();

  private readonly IClipboard _clipboard;
  private readonly Lock _compositedFrameLock = new();
  private readonly IDisposable _messageHandlerRegistration;
  private readonly IMessenger _messenger;
  private readonly IMetricsState _metricsState;
  private readonly IRemoteControlState _remoteControlState;
  private readonly IDisposable _remoteControlStateRegistration;
  private readonly ISnackbar _snackbar;
  private readonly IHubConnection<IViewerHub> _viewerHub;
  private readonly IViewerRemoteControlStream _viewerStream;

  [ObservableProperty]
  private CursorChangedDto? _activeCursor;
  private SKBitmap? _compositedFrame;
  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(IsBlockInputToggleEnabled))]
  private bool _isBlockInputBusy;
  [ObservableProperty]
  private double _selectedDisplayHeight;
  [ObservableProperty]
  private double _selectedDisplayWidth;

  public RemoteDisplayViewModel(
    IViewerRemoteControlStream viewerStream,
    IRemoteControlState remoteControlState,
    IMetricsState metricsState,
    IHubConnection<IViewerHub> viewerHub,
    IMessenger messenger,
    IClipboard clipboard,
    ISnackbar snackbar,
    ILogger<RemoteDisplayViewModel> logger)
  {
    _viewerStream = viewerStream;
    _remoteControlState = remoteControlState;
    _metricsState = metricsState;
    _viewerHub = viewerHub;
    _messenger = messenger;
    _clipboard = clipboard;
    _snackbar = snackbar;
    Logger = logger;
    _messageHandlerRegistration = _viewerStream.RegisterMessageHandler(this, HandleRemoteControlDtoReceived);
    _remoteControlStateRegistration = _remoteControlState.OnStateChanged(HandleRemoteControlStateChanged);
  }

  public event EventHandler? FrameQueued;

  public ObservableCollection<DisplayLayoutItem> DisplayItems { get; } = [];
  public bool HasMetricsData => _metricsState.CurrentMetrics is not null;
  public bool HasMultipleDisplays => DisplayItems.Count > 1;
  public bool IsAutoPanEnabled
  {
    get => _remoteControlState.IsAutoPanEnabled;
    set
    {
      if (_remoteControlState.IsAutoPanEnabled == value)
      {
        return;
      }

      _remoteControlState.IsAutoPanEnabled = value;
    }
  }
  public bool IsBlockInputToggleEnabled => !IsBlockInputBusy;
  public bool IsBlockUserInputEnabled
  {
    get => _remoteControlState.IsBlockUserInputEnabled;
    set
    {
      if (_remoteControlState.IsBlockUserInputEnabled == value)
      {
        return;
      }

      _ = ToggleBlockInput(value);
    }
  }
  public bool IsFitViewMode
  {
    get => ViewMode == ViewMode.Fit;
    set
    {
      if (value)
      {
        SetViewMode(ViewMode.Fit);
      }
    }
  }
  public bool IsKeyboardInputAuto
  {
    get => _remoteControlState.KeyboardInputMode == KeyboardInputMode.Auto;
    set
    {
      if (value)
      {
        SetKeyboardInputMode(KeyboardInputMode.Auto);
      }
    }
  }
  public bool IsKeyboardInputPhysical
  {
    get => _remoteControlState.KeyboardInputMode == KeyboardInputMode.Physical;
    set
    {
      if (value)
      {
        SetKeyboardInputMode(KeyboardInputMode.Physical);
      }
    }
  }
  public bool IsKeyboardInputVirtual
  {
    get => _remoteControlState.KeyboardInputMode == KeyboardInputMode.Virtual;
    set
    {
      if (value)
      {
        SetKeyboardInputMode(KeyboardInputMode.Virtual);
      }
    }
  }
  public bool IsMetricsEnabled
  {
    get => _remoteControlState.IsMetricsEnabled;
    set
    {
      if (_remoteControlState.IsMetricsEnabled == value)
      {
        return;
      }

      _remoteControlState.IsMetricsEnabled = value;
      OnPropertyChanged();
    }
  }
  public bool IsPrivacyScreenEnabled
  {
    get => _remoteControlState.IsPrivacyScreenEnabled;
    set
    {
      if (_remoteControlState.IsPrivacyScreenEnabled == value)
      {
        return;
      }

      _ = TogglePrivacyScreen(value);
      OnPropertyChanged();
    }
  }
  public bool IsScaleControlsVisible => ViewMode == ViewMode.Scale;
  public bool IsScaleViewMode
  {
    get => ViewMode == ViewMode.Scale;
    set
    {
      if (value)
      {
        SetViewMode(ViewMode.Scale);
      }
    }
  }
  public bool IsStretchViewMode
  {
    get => ViewMode == ViewMode.Stretch;
    set
    {
      if (value)
      {
        SetViewMode(ViewMode.Stretch);
      }
    }
  }
  public bool IsViewOnlyEnabled
  {
    get => _remoteControlState.IsViewOnlyEnabled;
    set
    {
      if (_remoteControlState.IsViewOnlyEnabled == value)
      {
        return;
      }

      _remoteControlState.IsViewOnlyEnabled = value;
      OnPropertyChanged();
    }
  }
  // This is public so ScreenRenderer can use it.
  public ILogger<RemoteDisplayViewModel> Logger { get; private set; }
  public double MaxRendererScale => _remoteControlState.MaxRendererScale;
  public IReadOnlyDictionary<string, string> MetricsExtraData =>
    _metricsState.CurrentMetrics?.ExtraData ?? _emptyExtraData;
  public double MetricsFps => _metricsState.CurrentMetrics?.Fps ?? 0;
  public TimeSpan MetricsLatency => _metricsState.CurrentLatency;
  public double MetricsMbpsIn => _metricsState.MbpsIn;
  public double MetricsMbpsOut => _metricsState.MbpsOut;
  public string MetricsMode => _metricsState.CurrentMetrics?.CaptureMode ?? string.Empty;
  public double MinRendererScale => _remoteControlState.MinRendererScale;
  public double RendererHeight
  {
    get
    {
      return ViewMode == ViewMode.Scale
        ? SelectedDisplayHeight * RendererScale
        : double.NaN;
    }
  }
  public HorizontalAlignment RendererHorizontalAlignment
  {
    get
    {
      return _remoteControlState.ViewMode switch
      {
        ViewMode.Fit => HorizontalAlignment.Stretch,
        ViewMode.Scale => HorizontalAlignment.Left,
        ViewMode.Stretch => HorizontalAlignment.Stretch,
        _ => HorizontalAlignment.Left
      };
    }
  }
  public double RendererScale
  {
    get => _remoteControlState.RendererScale;
    set
    {
      var clamped = Math.Clamp(value, MinRendererScale, MaxRendererScale);
      if (Math.Abs(_remoteControlState.RendererScale - clamped) < 0.001)
      {
        return;
      }

      _remoteControlState.RendererScale = clamped;
    }
  }
  public VerticalAlignment RendererVerticalAlignment
  {
    get
    {
      return _remoteControlState.ViewMode switch
      {
        ViewMode.Fit => VerticalAlignment.Stretch,
        ViewMode.Scale => VerticalAlignment.Top,
        ViewMode.Stretch => VerticalAlignment.Stretch,
        _ => VerticalAlignment.Top
      };
    }
  }
  public double RendererWidth
  {
    get
    {
      return ViewMode == ViewMode.Scale
        ? SelectedDisplayWidth * RendererScale
        : double.NaN;
    }
  }
  public bool ShowPrivacyScreenToggle =>
    _remoteControlState.CurrentSession?.Device.Platform == SystemPlatform.Windows;
  public bool ShowWindowsInputControls =>
    _remoteControlState.CurrentSession?.Device.Platform == SystemPlatform.Windows;
  public ViewMode ViewMode => _remoteControlState.ViewMode;

  public DisposableValue<SKBitmap?> AcquireCompositedFrame()
  {
    _compositedFrameLock.Enter();
    return new DisposableValue<SKBitmap?>(
      _compositedFrame, 
      () => _compositedFrameLock.Exit());
  }

  public async Task InvokeCtrlAltDel()
  {
    try
    {
      if (_remoteControlState.CurrentSession is not { } currentSession)
      {
        _snackbar.Add(Resources.RemoteControl_NoActiveSession, SnackbarSeverity.Error);
        return;
      }

      var invokeResult = await _viewerHub.Server.InvokeCtrlAltDel(
        currentSession.Device.Id,
        currentSession.TargetProcessId,
        currentSession.DesktopSessionType);

      if (!invokeResult.IsSuccess)
      {
        _snackbar.Add(string.Format(Resources.RemoteControl_FailedToSendCtrlAltDel, invokeResult.Reason), SnackbarSeverity.Error);
        return;
      }

      _snackbar.Add(Resources.RemoteControl_CtrlAltDelSent, SnackbarSeverity.Success);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while invoking Ctrl+Alt+Del.");
      _snackbar.Add(Resources.RemoteControl_ErrorSendingCtrlAltDel, SnackbarSeverity.Error);
    }
  }

  public async Task RequestClipboardText()
  {
    try
    {
      if (_remoteControlState.CurrentSession is not { } currentSession)
      {
        _snackbar.Add(Resources.RemoteControl_NoActiveSession, SnackbarSeverity.Error);
        return;
      }

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await _viewerStream.RequestClipboardText(currentSession.SessionId, cts.Token);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while requesting clipboard text.");
      _snackbar.Add(Resources.RemoteControl_ErrorReceivingClipboard, SnackbarSeverity.Error);
    }
  }

  public async Task SendClipboardText(string text)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(text))
      {
        _snackbar.Add(Resources.RemoteControl_ClipboardEmpty, SnackbarSeverity.Warning);
        return;
      }

      if (_remoteControlState.CurrentSession is not { } currentSession)
      {
        _snackbar.Add(Resources.RemoteControl_NoActiveSession, SnackbarSeverity.Error);
        return;
      }

      _snackbar.Add(Resources.RemoteControl_SendingClipboard, SnackbarSeverity.Info);
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await _viewerStream.SendClipboardText(text, currentSession.SessionId, cts.Token);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending clipboard text.");
      _snackbar.Add(Resources.RemoteControl_ErrorSendingClipboard, SnackbarSeverity.Error);
    }
  }

  public async Task SendKeyboardStateReset()
  {
    try
    {
      await _viewerStream.SendKeyboardStateReset(CancellationToken.None);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error sending keyboard reset event.");
    }
  }

  public async Task SendKeyEvent(string key, string code, bool isPressed, KeyEventModifiersDto modifiers)
  {
    if (_remoteControlState.IsViewOnlyEnabled)
    {
      _snackbar.Add(Resources.RemoteControl_InputSuppressedViewOnlyMode, SnackbarSeverity.Warning);
      return;
    }

    try
    {
      await _viewerStream.SendKeyEvent(
        key,
        code,
        isPressed,
        _remoteControlState.KeyboardInputMode,
        modifiers,
        CancellationToken.None);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error sending key event.");
    }
  }

  public async Task SendMouseButtonEvent(int button, bool isPressed, double percentX, double percentY)
  {
    if (_remoteControlState.IsViewOnlyEnabled)
    {
      _snackbar.Add(Resources.RemoteControl_InputSuppressedViewOnlyMode, SnackbarSeverity.Warning);
      return;
    }

    try
    {
      await _viewerStream.SendMouseButtonEvent(
        button,
        isPressed,
        percentX,
        percentY,
        CancellationToken.None);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error sending mouse button event.");
    }
  }

  public async Task SendPointerMove(double percentX, double percentY)
  {
    if (_remoteControlState.IsViewOnlyEnabled)
    {
      return;
    }

    try
    {
      await _viewerStream.SendPointerMove(percentX, percentY, CancellationToken.None);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error sending pointer move event.");
    }
  }

  public async Task SendWheelScroll(double percentX, double percentY, double scrollY, double scrollX)
  {
    if (_remoteControlState.IsViewOnlyEnabled)
    {
      return;
    }

    try
    {
      await _viewerStream.SendWheelScroll(percentX, percentY, scrollY, scrollX, CancellationToken.None);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error sending wheel scroll event.");
    }
  }

  public async Task TypeClipboardText(string text)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(text))
      {
        _snackbar.Add(Resources.RemoteControl_ClipboardEmpty, SnackbarSeverity.Warning);
        return;
      }

      _snackbar.Add(Resources.RemoteControl_SendingClipboardToType, SnackbarSeverity.Info);
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await _viewerStream.SendTypeText(text, cts.Token);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while typing clipboard text.");
      _snackbar.Add(Resources.RemoteControl_ErrorTypingClipboard, SnackbarSeverity.Error);
    }
  }

  protected override void Dispose(bool disposing)
  {
    base.Dispose(disposing);
    if (disposing)
    {
      _messageHandlerRegistration.Dispose();
      _remoteControlStateRegistration.Dispose();
      _compositedFrame?.Dispose();
      _compositedFrame = null;
    }
  }

  private static SnackbarSeverity ToSnackbarSeverity(MessageSeverity severity)
  {
    return severity switch
    {
      MessageSeverity.Warning => SnackbarSeverity.Warning,
      MessageSeverity.Error => SnackbarSeverity.Error,
      MessageSeverity.Success => SnackbarSeverity.Success,
      _ => SnackbarSeverity.Info,
    };
  }

  [RelayCommand]
  private async Task Disconnect()
  {
    await HandleDisconnectClicked();
  }

  private async Task DrawRegion(ScreenRegionDto dto)
  {
    try
    {
      using var imageStream = new MemoryStream(dto.EncodedImage);
      using var decodedBitmap = SKBitmap.Decode(imageStream);

      if (decodedBitmap is null)
      {
        Logger.LogWarning("Decoded screen region bitmap was null.");
        return;
      }

      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        EnsureCompositedFrameSize();
        if (_compositedFrame is null)
        {
          return;
        }

        using (_compositedFrameLock.EnterScope())
        {
          using var canvas = new SKCanvas(_compositedFrame);
          canvas.DrawBitmap(decodedBitmap, dto.X, dto.Y);
        }

        FrameQueued?.Invoke(this, EventArgs.Empty);
      }, DispatcherPriority.Render);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while drawing render frame.");
    }
  }

  private void EnsureCompositedFrameSize()
  {
    var width = (int)Math.Ceiling(SelectedDisplayWidth);
    var height = (int)Math.Ceiling(SelectedDisplayHeight);

    if (width <= 0 || height <= 0)
    {
      return;
    }
    
    using (_compositedFrameLock.EnterScope())
    {
      if (_compositedFrame?.Width == width && _compositedFrame.Height == height)
      {
        return;
      }

      _compositedFrame?.Dispose();
      _compositedFrame = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
    }
  }

  private async Task HandleClipboardTextReceived(ClipboardTextDto dto)
  {
    if (dto.SessionId != _remoteControlState.CurrentSession?.SessionId)
    {
      return;
    }

    await _clipboard.SetTextAsync(dto.Text);
    _snackbar.Add(Resources.RemoteControl_ClipboardTextReceived, SnackbarSeverity.Info);
  }

  private Task HandleCursorChanged(CursorChangedDto dto)
  {
    if (dto.SessionId != _remoteControlState.CurrentSession?.SessionId)
    {
      return Task.CompletedTask;
    }

    ActiveCursor = dto;
    return Task.CompletedTask;
  }

  private async Task HandleDisconnectClicked()
  {
    await _messenger.SendEvent(EventKinds.RemoteControlDisconnectRequested);
  }

  private async Task HandleDisplayDataReceived(DisplayDataDto dto)
  {
    _remoteControlState.DisplayData = dto.Displays;

    if (_remoteControlState.DisplayData.Length == 0)
    {
      _snackbar.Add(Resources.RemoteControl_NoDisplaysReceived, SnackbarSeverity.Error);
      await _messenger.SendEvent(EventKinds.RemoteControlDisconnectRequested);
      return;
    }

    var selectedDisplay = _remoteControlState.DisplayData
      .FirstOrDefault(x => x.IsPrimary)
      ?? _remoteControlState.DisplayData[0];

    _remoteControlState.SelectedDisplay = selectedDisplay;

    SelectedDisplayWidth = selectedDisplay.Width;
    SelectedDisplayHeight = selectedDisplay.Height;
    SyncDisplayItems();
  }

  private async Task HandleRemoteControlDtoReceived(DtoWrapper message)
  {
    try
    {
      switch (message.DtoType)
      {
        case DtoType.DisplayData:
          {
            var dto = message.GetPayload<DisplayDataDto>();
            await HandleDisplayDataReceived(dto);
            break;
          }
        case DtoType.ScreenRegion:
          {
            var dto = message.GetPayload<ScreenRegionDto>();
            await DrawRegion(dto);
            break;
          }
        case DtoType.ClipboardText:
          {
            var dto = message.GetPayload<ClipboardTextDto>();
            await HandleClipboardTextReceived(dto);
            break;
          }
        case DtoType.CursorChanged:
          {
            var dto = message.GetPayload<CursorChangedDto>();
            await HandleCursorChanged(dto);
            break;
          }
        case DtoType.WindowsSessionEnding:
          {
            _snackbar.Add(Resources.RemoteControl_WindowsSessionEnding, SnackbarSeverity.Warning);
            await _messenger.SendEvent(EventKinds.RemoteControlDisconnectRequested);
            break;
          }
        case DtoType.WindowsSessionSwitched:
          {
            _snackbar.Add(Resources.RemoteControl_WindowsSessionSwitched, SnackbarSeverity.Info);
            break;
          }
        case DtoType.CaptureMetricsChanged:
          {
            var dto = message.GetPayload<CaptureMetricsDto>();
            _metricsState.CurrentMetrics = dto;
            _metricsState.CurrentLatency = _viewerStream.CurrentLatency;
            _metricsState.MbpsIn = _viewerStream.GetMbpsIn();
            _metricsState.MbpsOut = _viewerStream.GetMbpsOut();
            OnPropertyChanged(nameof(HasMetricsData));
            OnPropertyChanged(nameof(MetricsFps));
            OnPropertyChanged(nameof(MetricsLatency));
            OnPropertyChanged(nameof(MetricsMode));
            OnPropertyChanged(nameof(MetricsMbpsIn));
            OnPropertyChanged(nameof(MetricsMbpsOut));
            OnPropertyChanged(nameof(MetricsExtraData));
            break;
          }
        case DtoType.ToastNotification:
          {
            var dto = message.GetPayload<ToastNotificationDto>();
            _snackbar.Add(dto.Message, ToSnackbarSeverity(dto.Severity));
            break;
          }
        case DtoType.BlockInputResult:
          {
            var dto = message.GetPayload<BlockInputResultDto>();
            _remoteControlState.IsBlockUserInputEnabled = dto.FinalState;
            IsBlockInputBusy = false;
            await _remoteControlState.NotifyStateChanged();
            _snackbar.Add(
              dto.IsSuccess
                ? dto.FinalState ? Resources.RemoteControl_BlockInputEnabled : Resources.RemoteControl_BlockInputDisabled
                : dto.FinalState ? Resources.RemoteControl_FailedToDisableBlockInput : Resources.RemoteControl_FailedToEnableBlockInput,
              dto.IsSuccess ? SnackbarSeverity.Success : SnackbarSeverity.Error);
            break;
          }
        case DtoType.PrivacyScreenResult:
          {
            var dto = message.GetPayload<PrivacyScreenResultDto>();
            _remoteControlState.IsPrivacyScreenEnabled = dto.FinalState;
            await _remoteControlState.NotifyStateChanged();
            _snackbar.Add(
              dto.IsSuccess
                ? dto.FinalState ? Resources.RemoteControl_PrivacyScreenEnabled : Resources.RemoteControl_PrivacyScreenDisabled
                : dto.FinalState ? Resources.RemoteControl_FailedToDisablePrivacyScreen : Resources.RemoteControl_FailedToEnablePrivacyScreen,
              dto.IsSuccess ? SnackbarSeverity.Success : SnackbarSeverity.Error);
            break;
          }
        case DtoType.SessionDisconnectRequested:
          {
            _snackbar.Add(Resources.RemoteControl_RemoteUserRequestedDisconnection, SnackbarSeverity.Info);
            await _messenger.SendEvent(EventKinds.RemoteControlDisconnectRequested);
            break;
          }
        case DtoType.RemoteControlSessionError:
          {
            var dto = message.GetPayload<RemoteControlSessionErrorDto>();
            await HandleRemoteControlSessionError(dto);
            break;
          }
        default:
          Logger.LogWarning("Received unsupported DTO type: {DtoType}", message.DtoType);
          _snackbar.Add(string.Format(Resources.RemoteControl_UnsupportedDtoType, message.DtoType), SnackbarSeverity.Warning);
          break;
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while handling remote control DTO. Type: {DtoType}", message.DtoType);
    }
  }

  private Task HandleRemoteControlSessionError(RemoteControlSessionErrorDto dto)
  {
    _snackbar.Add(dto.Message, SnackbarSeverity.Error);

    if (dto.IsFatal)
    {
      _ = _messenger.SendEvent(EventKinds.RemoteControlDisconnectRequested);
    }

    return Task.CompletedTask;
  }

  private Task HandleRemoteControlStateChanged()
  {
    SyncDisplayItems();
    OnPropertyChanged(nameof(RendererScale));
    OnPropertyChanged(nameof(IsAutoPanEnabled));
    OnPropertyChanged(nameof(IsBlockUserInputEnabled));
    OnPropertyChanged(nameof(IsFitViewMode));
    OnPropertyChanged(nameof(IsKeyboardInputAuto));
    OnPropertyChanged(nameof(IsKeyboardInputPhysical));
    OnPropertyChanged(nameof(IsKeyboardInputVirtual));
    OnPropertyChanged(nameof(IsMetricsEnabled));
    OnPropertyChanged(nameof(IsPrivacyScreenEnabled));
    OnPropertyChanged(nameof(IsScaleControlsVisible));
    OnPropertyChanged(nameof(IsScaleViewMode));
    OnPropertyChanged(nameof(IsStretchViewMode));
    OnPropertyChanged(nameof(IsViewOnlyEnabled));
    OnPropertyChanged(nameof(ViewMode));
    OnPropertyChanged(nameof(RendererHorizontalAlignment));
    OnPropertyChanged(nameof(RendererVerticalAlignment));
    OnPropertyChanged(nameof(RendererWidth));
    OnPropertyChanged(nameof(RendererHeight));
    OnPropertyChanged(nameof(ShowWindowsInputControls));
    OnPropertyChanged(nameof(ShowPrivacyScreenToggle));
    return Task.CompletedTask;
  }

  partial void OnSelectedDisplayHeightChanged(double value)
  {
    OnPropertyChanged(nameof(RendererHeight));
  }

  partial void OnSelectedDisplayWidthChanged(double value)
  {
    OnPropertyChanged(nameof(RendererWidth));
  }

  [RelayCommand]
  private async Task SelectDisplay(DisplayLayoutItem? displayItem)
  {
    if (displayItem is null)
    {
      return;
    }

    try
    {
      using (_compositedFrameLock.EnterScope())
      {
        _remoteControlState.SelectedDisplay = displayItem.Display;
        SelectedDisplayWidth = displayItem.Display.Width;
        SelectedDisplayHeight = displayItem.Display.Height;
        UpdateSelectedDisplayState();
      }

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await _viewerStream.SendChangeDisplaysRequest(displayItem.DisplayId, cts.Token);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while changing displays.");
    }
  }

  private void SetKeyboardInputMode(KeyboardInputMode mode)
  {
    if (_remoteControlState.KeyboardInputMode == mode)
    {
      return;
    }

    _remoteControlState.KeyboardInputMode = mode;
    OnPropertyChanged(nameof(IsKeyboardInputAuto));
    OnPropertyChanged(nameof(IsKeyboardInputPhysical));
    OnPropertyChanged(nameof(IsKeyboardInputVirtual));
  }

  private void SetViewMode(ViewMode viewMode)
  {
    if (_remoteControlState.ViewMode == viewMode)
    {
      return;
    }

    _remoteControlState.ViewMode = viewMode;
    OnPropertyChanged(nameof(IsFitViewMode));
    OnPropertyChanged(nameof(IsScaleControlsVisible));
    OnPropertyChanged(nameof(IsScaleViewMode));
    OnPropertyChanged(nameof(IsStretchViewMode));
    OnPropertyChanged(nameof(ViewMode));
  }

  private void SyncDisplayItems()
  {
    var displays = _remoteControlState.DisplayData ?? [];

    DisplayItems.Clear();
    foreach (var display in displays)
    {
      DisplayItems.Add(new DisplayLayoutItem(display, item => SelectDisplay(item)));
    }

    OnPropertyChanged(nameof(HasMultipleDisplays));

    UpdateDisplayLayout();
    UpdateSelectedDisplayState();
  }

  private async Task ToggleBlockInput(bool isEnabled)
  {
    try
    {
      if (_viewerStream.State != System.Net.WebSockets.WebSocketState.Open)
      {
        _snackbar.Add(Resources.RemoteControl_NoActiveSession, SnackbarSeverity.Error);
        return;
      }

      IsBlockInputBusy = true;
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await _viewerStream.SendToggleBlockInput(isEnabled, cts.Token);
    }
    catch (Exception ex)
    {
      IsBlockInputBusy = false;
      Logger.LogError(ex, "Error while toggling block input.");
      _snackbar.Add(Resources.RemoteControl_ErrorTogglingBlockInput, SnackbarSeverity.Error);
      _remoteControlState.IsBlockUserInputEnabled = !isEnabled;
      await _remoteControlState.NotifyStateChanged();
    }
  }

  private async Task TogglePrivacyScreen(bool isEnabled)
  {
    try
    {
      _remoteControlState.IsPrivacyScreenEnabled = isEnabled;
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await _viewerStream.SendTogglePrivacyScreen(isEnabled, cts.Token);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while toggling privacy screen.");
    }
  }

  private void UpdateDisplayLayout()
  {
    if (DisplayItems.Count == 0)
    {
      return;
    }

    var minLeft = DisplayItems.Min(x => x.Display.Left);
    var minTop = DisplayItems.Min(x => x.Display.Top);
    var maxRight = DisplayItems.Max(x => x.Display.Left + x.Display.Width);
    var maxBottom = DisplayItems.Max(x => x.Display.Top + x.Display.Height);

    var totalWidth = maxRight - minLeft;
    var totalHeight = maxBottom - minTop;

    var scaleX = totalWidth == 0 ? 1 : DisplayLayoutContainerWidth / totalWidth;
    var scaleY = totalHeight == 0 ? 1 : DisplayLayoutContainerHeight / totalHeight;
    var scale = Math.Min(scaleX, scaleY);

    foreach (var display in DisplayItems)
    {
      display.LayoutLeft = (display.Display.Left - minLeft) * scale;
      display.LayoutTop = (display.Display.Top - minTop) * scale;
      display.LayoutWidth = display.Display.Width * scale;
      display.LayoutHeight = display.Display.Height * scale;
    }
  }

  private void UpdateSelectedDisplayState()
  {
    var selectedDisplayId = _remoteControlState.SelectedDisplay?.DisplayId;
    foreach (var display in DisplayItems)
    {
      display.IsSelected = string.Equals(display.DisplayId, selectedDisplayId, StringComparison.Ordinal);
    }
  }
}