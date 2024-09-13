using ControlR.Libraries.Clients.Services;
using ControlR.Libraries.Shared.Dtos.SidecarDtos;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Services.Buffers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;

namespace ControlR.Web.Client.Services;

public interface IViewerStreamingClient : IStreamingClient, IClosable
{
    Task SendChangeDisplaysRequest(string displayId, CancellationToken cancellationToken);
    Task SendClipboardText(string text, Guid sessionId, CancellationToken cancellationToken);

    Task SendCloseStreamingSession(CancellationToken cancellationToken);
    Task SendKeyboardStateReset(CancellationToken cancellationToken);
    Task SendKeyEvent(string key, bool isPressed, CancellationToken cancellationToken);
    Task SendMouseButtonEvent(int button, bool isPressed, double percentX, double percentY, CancellationToken cancellationToken);
    Task SendMouseClick(int button, bool isDoubleClick, double percentX, double percentY, CancellationToken cancellationToken);
    Task SendPointerMove(double percentX, double percentY, CancellationToken cancellationToken);
    Task SendTypeText(string text, CancellationToken cancellationToken);
    Task SendWheelScroll(double percentX, double percentY, double scrollY, double scrollX, CancellationToken cancellationToken);
}

public class ViewerStreamingClient(
    IMessenger messenger,
    IMemoryProvider _memoryProvider,
    IDelayer _delayer,
    ILogger<ViewerStreamingClient> _logger,
    ILogger<StreamingClient> _baseLogger) : StreamingClient(messenger, _memoryProvider, _baseLogger), IViewerStreamingClient
{
    public async Task SendChangeDisplaysRequest(string displayId, CancellationToken cancellationToken)
    {
        await TrySend(
            async () =>
            {
                var dto = new ChangeDisplaysDto(displayId);
                var wrapper = DtoWrapper.Create(dto, DtoType.ChangeDisplays);
                await Send(wrapper, cancellationToken);
            });
    }

    public async Task SendClipboardText(string text, Guid sessionId, CancellationToken cancellationToken)
    {
        await TrySend(
             async () =>
             {
                 var dto = new ClipboardChangeDto(text, sessionId);
                 var wrapper = DtoWrapper.Create(dto, DtoType.ClipboardChanged);
                 await Send(wrapper, cancellationToken);
             });
    }

    public async Task SendCloseStreamingSession(CancellationToken cancellationToken)
    {
        await TrySend(
            async () =>
            {
                var dto = new CloseStreamingSessionRequestDto();
                var wrapper = DtoWrapper.Create(dto, DtoType.CloseStreamingSession);
                await Send(wrapper, cancellationToken);
            });
    }
    public async Task SendKeyboardStateReset(CancellationToken cancellationToken)
    {
        await TrySend(
              async () =>
              {
                  var dto = new ResetKeyboardStateDto();
                  var wrapper = DtoWrapper.Create(dto, DtoType.ResetKeyboardState);
                  await Send(wrapper, cancellationToken);
              });
    }

    public async Task SendKeyEvent(string key, bool isPressed, CancellationToken cancellationToken)
    {
        await TrySend(
              async () =>
              {
                  var dto = new KeyEventDto(key, isPressed);
                  var wrapper = DtoWrapper.Create(dto, DtoType.KeyEvent);
                  await Send(wrapper, cancellationToken);
              });
    }

    public async Task SendMouseButtonEvent(
        int button,
        bool isPressed,
        double percentX,
        double percentY,
        CancellationToken cancellationToken)
    {
        await TrySend(
            async () =>
            {
                var dto = new MouseButtonEventDto(button, isPressed, percentX, percentY);
                var wrapper = DtoWrapper.Create(dto, DtoType.MouseButtonEvent);
                await Send(wrapper, cancellationToken);
            });
    }

    public async Task SendMouseClick(int button, bool isDoubleClick, double percentX, double percentY, CancellationToken cancellationToken)
    {
        await TrySend(
             async () =>
             {
                 var dto = new MouseClickDto(button, isDoubleClick, percentX, percentY);
                 var wrapper = DtoWrapper.Create(dto, DtoType.MouseClick);
                 await Send(wrapper, cancellationToken);
             });
    }

    public async Task SendPointerMove(double percentX, double percentY, CancellationToken cancellationToken)
    {
        await TrySend(
            async () =>
            {
                var dto = new MovePointerDto(percentX, percentY);
                var wrapper = DtoWrapper.Create(dto, DtoType.MovePointer);
                await Send(wrapper, cancellationToken);
            });
    }

    public async Task SendTypeText(string text, CancellationToken cancellationToken)
    {
        await TrySend(
             async () =>
             {
                 var dto = new TypeTextDto(text);
                 var wrapper = DtoWrapper.Create(dto, DtoType.TypeText);
                 await Send(wrapper, cancellationToken);
             });
    }
    public async Task SendWheelScroll(double percentX, double percentY, double scrollY, double scrollX, CancellationToken cancellationToken)
    {
        await TrySend(
            async () =>
            {
                var dto = new WheelScrollDto(percentX, percentY, scrollY, scrollX);
                var wrapper = DtoWrapper.Create(dto, DtoType.WheelScroll);
                await Send(wrapper, cancellationToken);
            });
    }

    private async Task TrySend(Func<Task> func, [CallerMemberName] string callerName = "")
    {
        try
        {
            using var _ = _logger.BeginScope(callerName);
            await WaitForConnection();
            await func.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while invoking hub method.");
        }
    }

    private async Task WaitForConnection()
    {
        if (Client.State == WebSocketState.Open)
        {
            return;
        }

        await _delayer.WaitForAsync(
            () => Client.State == WebSocketState.Open || IsDisposed,
            TimeSpan.FromSeconds(30),
            pollingMs: 100);
    }
}
