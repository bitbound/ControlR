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

    messenger.Register<DtoReceivedMessage<RemoteControlDtoWrapper>>(this, HandleRemoteControlDtoReceived);
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
        var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.RequestClipboardText);
        return await InvokeDto(wrapper, cancellationToken);
      });
  }

  public async Task RequestKeyFrame(CancellationToken cancellationToken)
  {
    await TrySend(
      async () =>
      {
        var dto = new RequestKeyFrameDto();
        var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.RequestKeyFrame);
        await SendDto(wrapper, cancellationToken);
      });
  }

  public async Task<HubResult> SendCaptureSettings(UpdateCaptureSettingsDto settings, CancellationToken cancellationToken)
  {
    return await TryInvoke(
      async () =>
      {
        var wrapper = RemoteControlDtoWrapper.Create(settings, RemoteControlDtoType.UpdateCaptureSettings);
        return await InvokeDto(wrapper, cancellationToken);
      });
  }

  public async Task<HubResult> SendChangeDisplaysRequest(string displayId, CancellationToken cancellationToken)
  {
    return await TryInvoke(
      async () =>
      {
        var dto = new ChangeDisplaysDto(displayId);
        var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.ChangeDisplays);
        return await InvokeDto(wrapper, cancellationToken);
      });
  }

  public async Task<HubResult> SendClipboardText(string text, Guid sessionId, CancellationToken cancellationToken)
  {
    return await TryInvoke(
      async () =>
      {
        var dto = new ClipboardTextDto(text, sessionId);
        var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.ClipboardText);
        return await InvokeDto(wrapper, cancellationToken);
      });
  }

  public async Task SendCloseStreamingSession(CancellationToken cancellationToken)
  {
    await TrySend(
      async () =>
      {
        var dto = new CloseStreamingSessionRequestDto();
        var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.CloseRemoteControlSession);
        await SendDto(wrapper, cancellationToken);
      });
  }

  public async Task SendKeyboardStateReset(CancellationToken cancellationToken)
  {
    await TrySend(
      async () =>
      {
        var dto = new ResetKeyboardStateDto();
        var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.ResetKeyboardState);
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
        var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.KeyEvent);
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
        var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.MouseButtonEvent);
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
        var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.MouseClick);
        await SendDto(wrapper, cancellationToken);
      });
  }

  public async Task SendPointerMove(double normalizedX, double normalizedY, CancellationToken cancellationToken)
  {
    await TrySend(
      async () =>
      {
        var dto = new MovePointerDto(normalizedX, normalizedY);
        var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.MovePointer);
        await SendDto(wrapper, cancellationToken);
      });
  }

  public async Task<HubResult> SendToggleBlockInput(bool isEnabled, CancellationToken cancellationToken)
  {
    return await TryInvoke(
      async () =>
      {
        var dto = new ToggleBlockInputDto(isEnabled);
        var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.ToggleBlockInput);
        return await InvokeDto(wrapper, cancellationToken);
      });
  }

  public async Task<HubResult> SendTogglePrivacyScreen(bool isEnabled, CancellationToken cancellationToken)
  {
    return await TryInvoke(
      async () =>
      {
        var dto = new TogglePrivacyScreenDto(isEnabled);
        var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.TogglePrivacyScreen);
        return await InvokeDto(wrapper, cancellationToken);
      });
  }

  public async Task SendTypeText(string text, CancellationToken cancellationToken)
  {
    await TrySend(
      async () =>
      {
        var dto = new TypeTextDto(text);
        var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.TypeText);
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
        var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.WheelScroll);
        await SendDto(wrapper, cancellationToken);
      });
  }

  private static SendRemoteControlDtoToAgentRequestDto CreateRequest(
    RemoteControlSession session,
    RemoteControlDtoWrapper wrapper) =>
    new(
      session.Device.Id,
      session.TargetProcessId,
      session.SessionId,
      wrapper);

  private Task HandleRemoteControlDtoReceived(object subscriber, DtoReceivedMessage<RemoteControlDtoWrapper> message)
  {
    return InvokeMessageHandlers(message.Dto);
  }

  private async Task<HubResult> InvokeDto(RemoteControlDtoWrapper wrapper, CancellationToken cancellationToken)
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

  private async Task SendDto(RemoteControlDtoWrapper wrapper, CancellationToken cancellationToken)
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
