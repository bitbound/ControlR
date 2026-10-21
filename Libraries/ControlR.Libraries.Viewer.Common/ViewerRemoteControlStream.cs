using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Libraries.WebSocketRelay.Client;

namespace ControlR.Libraries.Viewer.Common;

public interface IViewerRemoteControlStream : IManagedRelayStream
{
  Task Connect(
    RemoteControlSession session,
    Uri websocketUri,
    Action<ClientWebSocketOptions> configureOptions,
    CancellationToken cancellationToken);
  Task Connect(
    RemoteControlSession session,
    Uri websocketUri,
    CancellationToken cancellationToken);
  new Task Connect(Uri websocketUri, CancellationToken cancellationToken);
  new Task Connect(Uri websocketUri, Action<ClientWebSocketOptions> configureOptions, CancellationToken cancellationToken);
  Task<HubResult> RequestClipboardText(Guid sessionId, CancellationToken cancellationToken);
  Task RequestKeyFrame(CancellationToken cancellationToken);
  Task<HubResult> SendCaptureSettings(UpdateCaptureSettingsDto settings, CancellationToken cancellationToken);
  Task<HubResult> SendChangeDisplaysRequest(string displayId, CancellationToken cancellationToken);
  Task<HubResult> SendClipboardText(string text, Guid sessionId, CancellationToken cancellationToken);
  Task SendCloseStreamingSession(CancellationToken cancellationToken);
  Task SendKeyboardStateReset(CancellationToken cancellationToken);
  Task SendKeyEvent(
    string key,
    string? code,
    bool isPressed,
    KeyboardInputMode inputMode,
    KeyEventModifiersDto modifiers,
    CancellationToken cancellationToken);
  Task SendMouseButtonEvent(int button, bool isPressed, double normalizedX, double normalizedY,
    CancellationToken cancellationToken);
  Task SendMouseClick(int button, bool isDoubleClick, double normalizedX, double normalizedY,
    CancellationToken cancellationToken);
  Task SendPointerMove(double normalizedX, double normalizedY, CancellationToken cancellationToken);
  Task<HubResult> SendToggleBlockInput(bool isEnabled, CancellationToken cancellationToken);
  Task<HubResult> SendTogglePrivacyScreen(bool isEnabled, CancellationToken cancellationToken);
  Task SendTypeText(string text, CancellationToken cancellationToken);
  Task SendWheelScroll(double normalizedX, double normalizedY, double scrollY, double scrollX,
    CancellationToken cancellationToken);
}

public class ViewerRemoteControlStream : ManagedRelayStream, IViewerRemoteControlStream
{
  private readonly ILogger<ViewerRemoteControlStream> _logger;
  private readonly IHubConnection<IViewerHub> _viewerHub;
  private MovePointerDto? _pendingPointerMove;
  private int _pointerMovePumpRunning;
  private RemoteControlSession? _session;

  public ViewerRemoteControlStream(
    TimeProvider timeProvider,
    IMessenger messenger,
    IMemoryProvider memoryProvider,
    IWaiter waiter,
    IStreamMetrics streamMetrics,
    IHubConnection<IViewerHub> viewerHub,
    ILogger<ViewerRemoteControlStream> logger)
    : base(timeProvider, messenger, memoryProvider, waiter, streamMetrics, logger)
  {
    _logger = logger;
    _viewerHub = viewerHub;

    messenger.Register<DtoReceivedMessage<RemoteControlEventWrapper>>(this, HandleRemoteControlDtoReceived);
  }

  public Task Connect(
    RemoteControlSession session,
    Uri websocketUri,
    CancellationToken cancellationToken) =>
    Connect(session, websocketUri, _ => { }, cancellationToken);

  public Task Connect(
    RemoteControlSession session,
    Uri websocketUri,
    Action<ClientWebSocketOptions> configureOptions,
    CancellationToken cancellationToken)
  {
    _session = session;
    return base.Connect(websocketUri, configureOptions, cancellationToken);
  }

  public new Task Connect(Uri websocketUri, CancellationToken cancellationToken) =>
    throw new InvalidOperationException("Use the overload that accepts a remote control session.");

  public new Task Connect(
    Uri websocketUri,
    Action<ClientWebSocketOptions> configureOptions,
    CancellationToken cancellationToken) =>
    throw new InvalidOperationException("Use the overload that accepts a remote control session.");

  public async Task<HubResult> RequestClipboardText(Guid sessionId, CancellationToken cancellationToken)
  {
    return await TryInvoke(
      async () =>
      {
        var dto = new RequestClipboardTextDto();
        var wrapper = RemoteControlCommandWrapper.Create(dto, RemoteControlCommandDtoType.RequestClipboardText);
        return await InvokeDto(wrapper, cancellationToken);
      });
  }

  public async Task RequestKeyFrame(CancellationToken cancellationToken)
  {
    await TrySend(
      async () =>
      {
        var dto = new RequestKeyFrameDto();
        var wrapper = RemoteControlCommandWrapper.Create(dto, RemoteControlCommandDtoType.RequestKeyFrame);
        await SendDto(wrapper, cancellationToken);
      });
  }

  public async Task<HubResult> SendCaptureSettings(UpdateCaptureSettingsDto settings, CancellationToken cancellationToken)
  {
    return await TryInvoke(
      async () =>
      {
        var wrapper = RemoteControlCommandWrapper.Create(settings, RemoteControlCommandDtoType.UpdateCaptureSettings);
        return await InvokeDto(wrapper, cancellationToken);
      });
  }

  public async Task<HubResult> SendChangeDisplaysRequest(string displayId, CancellationToken cancellationToken)
  {
    return await TryInvoke(
      async () =>
      {
        var dto = new ChangeDisplaysDto(displayId);
        var wrapper = RemoteControlCommandWrapper.Create(dto, RemoteControlCommandDtoType.ChangeDisplays);
        return await InvokeDto(wrapper, cancellationToken);
      });
  }

  public async Task<HubResult> SendClipboardText(string text, Guid sessionId, CancellationToken cancellationToken)
  {
    return await TryInvoke(
      async () =>
      {
        var dto = new ClipboardTextDto(text, sessionId);
        var wrapper = RemoteControlCommandWrapper.Create(dto, RemoteControlCommandDtoType.ClipboardText);
        return await InvokeDto(wrapper, cancellationToken);
      });
  }

  public async Task SendCloseStreamingSession(CancellationToken cancellationToken)
  {
    await TrySend(
      async () =>
      {
        var dto = new CloseStreamingSessionRequestDto();
        var wrapper = RemoteControlCommandWrapper.Create(dto, RemoteControlCommandDtoType.CloseRemoteControlSession);
        await SendDto(wrapper, cancellationToken);
      });
  }

  public async Task SendKeyboardStateReset(CancellationToken cancellationToken)
  {
    await TrySend(
      async () =>
      {
        var dto = new ResetKeyboardStateDto();
        var wrapper = RemoteControlCommandWrapper.Create(dto, RemoteControlCommandDtoType.ResetKeyboardState);
        await SendDto(wrapper, cancellationToken);
      });
  }

  public async Task SendKeyEvent(
    string key,
    string? code,
    bool isPressed,
    KeyboardInputMode inputMode,
    KeyEventModifiersDto modifiers,
    CancellationToken cancellationToken)
  {
    await TrySend(
      async () =>
      {
        var dto = new KeyEventDto(key, code ?? string.Empty, isPressed, inputMode, modifiers);
        var wrapper = RemoteControlCommandWrapper.Create(dto, RemoteControlCommandDtoType.KeyEvent);
        await SendDto(wrapper, cancellationToken);
      });
  }

  public async Task SendMouseButtonEvent(
    int button,
    bool isPressed,
    double normalizedX,
    double normalizedY,
    CancellationToken cancellationToken)
  {
    await TrySend(
      async () =>
      {
        var dto = new MouseButtonEventDto(button, isPressed, normalizedX, normalizedY);
        var wrapper = RemoteControlCommandWrapper.Create(dto, RemoteControlCommandDtoType.MouseButtonEvent);
        await SendDto(wrapper, cancellationToken);
      });
  }

  public async Task SendMouseClick(int button, bool isDoubleClick, double normalizedX, double normalizedY,
    CancellationToken cancellationToken)
  {
    await TrySend(
      async () =>
      {
        var dto = new MouseClickDto(button, isDoubleClick, normalizedX, normalizedY);
        var wrapper = RemoteControlCommandWrapper.Create(dto, RemoteControlCommandDtoType.MouseClick);
        await SendDto(wrapper, cancellationToken);
      });
  }

  public Task SendPointerMove(double normalizedX, double normalizedY, CancellationToken cancellationToken)
  {
    // Pointer moves arrive at input rate and each one is superseded by the next, so a backlog of
    // them only delays the commands queued behind it.  Keep only the newest position and let a
    // single sender drain it.  Discrete events carry their own coordinates, so they reposition
    // the pointer themselves and do not depend on an earlier move having landed.
    Volatile.Write(ref _pendingPointerMove, new MovePointerDto(normalizedX, normalizedY));

    if (Interlocked.Exchange(ref _pointerMovePumpRunning, 1) == 1)
    {
      return Task.CompletedTask;
    }

    return PumpPointerMovesAsync(cancellationToken);
  }

  public async Task<HubResult> SendToggleBlockInput(bool isEnabled, CancellationToken cancellationToken)
  {
    return await TryInvoke(
      async () =>
      {
        var dto = new ToggleBlockInputDto(isEnabled);
        var wrapper = RemoteControlCommandWrapper.Create(dto, RemoteControlCommandDtoType.ToggleBlockInput);
        return await InvokeDto(wrapper, cancellationToken);
      });
  }

  public async Task<HubResult> SendTogglePrivacyScreen(bool isEnabled, CancellationToken cancellationToken)
  {
    return await TryInvoke(
      async () =>
      {
        var dto = new TogglePrivacyScreenDto(isEnabled);
        var wrapper = RemoteControlCommandWrapper.Create(dto, RemoteControlCommandDtoType.TogglePrivacyScreen);
        return await InvokeDto(wrapper, cancellationToken);
      });
  }

  public async Task SendTypeText(string text, CancellationToken cancellationToken)
  {
    await TrySend(
      async () =>
      {
        var dto = new TypeTextDto(text);
        var wrapper = RemoteControlCommandWrapper.Create(dto, RemoteControlCommandDtoType.TypeText);
        await SendDto(wrapper, cancellationToken);
      });
  }

  public async Task SendWheelScroll(double normalizedX, double normalizedY, double scrollY, double scrollX,
    CancellationToken cancellationToken)
  {
    await TrySend(
      async () =>
      {
        var dto = new WheelScrollDto(normalizedX, normalizedY, scrollY, scrollX);
        var wrapper = RemoteControlCommandWrapper.Create(dto, RemoteControlCommandDtoType.WheelScroll);
        await SendDto(wrapper, cancellationToken);
      });
  }

  private static SendRemoteControlDtoToAgentRequestDto CreateRequest(
    RemoteControlSession session,
    RemoteControlCommandWrapper wrapper) =>
    new(
      session.Device.Id,
      session.TargetProcessId,
      session.SessionId,
      wrapper);

  private Task HandleRemoteControlDtoReceived(object subscriber, DtoReceivedMessage<RemoteControlEventWrapper> message)
  {
    return InvokeMessageHandlers(message.Dto);
  }

  private async Task<HubResult> InvokeDto(RemoteControlCommandWrapper wrapper, CancellationToken cancellationToken)
  {
    if (_session is not { } session)
    {
      return HubResult.Fail("Remote control session has not been initialized.");
    }

    await _viewerHub.WaitForConnected(cancellationToken);

    var request = CreateRequest(session, wrapper);
    return await _viewerHub.Server
      .InvokeRemoteControlDtoOnAgent(request)
      .WaitAsync(cancellationToken);
  }

  private async Task PumpPointerMovesAsync(CancellationToken cancellationToken)
  {
    try
    {
      while (Interlocked.Exchange(ref _pendingPointerMove, null) is { } move)
      {
        await TrySend(
          async () =>
          {
            var wrapper = RemoteControlCommandWrapper.Create(move, RemoteControlCommandDtoType.MovePointer);
            await SendDto(wrapper, cancellationToken);
          });
      }
    }
    finally
    {
      Volatile.Write(ref _pointerMovePumpRunning, 0);

      // A position that landed between the last drain and clearing the flag would otherwise be
      // stranded, so hand the pump to whoever claims the flag next.
      if (Volatile.Read(ref _pendingPointerMove) is not null &&
        Interlocked.Exchange(ref _pointerMovePumpRunning, 1) == 0)
      {
        _ = PumpPointerMovesAsync(cancellationToken);
      }
    }
  }

  private async Task SendDto(RemoteControlCommandWrapper wrapper, CancellationToken cancellationToken)
  {
    if (_session is not { } session)
    {
      throw new InvalidOperationException("Remote control session has not been initialized.");
    }

    // The typed proxy awaits a completion message for any Task-returning method, so the
    // fire-and-forget path has to go through Send() instead of the Server property.
    var request = CreateRequest(session, wrapper);
    await _viewerHub.Send(
      nameof(IViewerHub.SendRemoteControlDtoToAgent),
      [request],
      cancellationToken);
  }

  private async Task<HubResult> TryInvoke(Func<Task<HubResult>> func, [CallerMemberName] string callerName = "")
  {
    try
    {
      using var _ = _logger.BeginScope(callerName);
      return await func.Invoke();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while invoking remote control method.");
      return HubResult.Fail(string.IsNullOrWhiteSpace(ex.Message)
        ? "An error occurred while sending the remote control command."
        : ex.Message);
    }
  }

  private async Task TrySend(Func<Task> func, [CallerMemberName] string callerName = "")
  {
    try
    {
      using var _ = _logger.BeginScope(callerName);
      await func.Invoke();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending remote control message.");
    }
  }
}
