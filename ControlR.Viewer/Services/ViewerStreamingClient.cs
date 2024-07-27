using ControlR.Libraries.Shared.Dtos.SidecarDtos;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;

namespace ControlR.Viewer.Services;

public interface IViewerStreamingClient : IClosable
{
    Task Connect(Uri websocketUri, CancellationToken cancellationToken);

    ValueTask DisposeAsync();

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

public sealed class ViewerStreamingClient(
    IServiceProvider _serviceProvider,
    IKeyProvider _keyProvider,
    IAppState _appState,
    IDelayer _delayer,
    ILogger<ViewerStreamingClient> _logger) : Closable(_logger), IAsyncDisposable, IViewerStreamingClient
{
    private bool _isDisposed;
    private IDisposable? _clientOnCloseRegistration;
    private IStreamingClient? _client;
    private IStreamingClient Client => _client ?? throw new InvalidOperationException("Client has not been initialized.");

    public async Task Connect(Uri websocketUri, CancellationToken cancellationToken)
    {
        _clientOnCloseRegistration?.Dispose();

        if (_client is not null)
        {
            await _client.DisposeAsync();
        }

        _client = _serviceProvider.GetRequiredService<IStreamingClient>();
        await _client.Connect(websocketUri, cancellationToken);
        _clientOnCloseRegistration =  _client.OnClose(Close);
    }


    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        // We don't need to invoke the callback if we're disposing.
        _clientOnCloseRegistration?.Dispose();
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }
    }

    public async Task SendChangeDisplaysRequest(string displayId, CancellationToken cancellationToken)
    {
        await TrySend(
            async () =>
            {
                var dto = new ChangeDisplaysDto(displayId);
                var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.ChangeDisplays, _appState.PrivateKey);
                await Client.Send(signedDto, cancellationToken);
            });
    }

    public async Task SendClipboardText(string text, Guid sessionId, CancellationToken cancellationToken)
    {
        await TrySend(
             async () =>
             {
                 var dto = new ClipboardChangeDto(text, sessionId);
                 var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.ClipboardChanged, _appState.PrivateKey);
                 await Client.Send(signedDto, cancellationToken);
             });
    }

    public async Task SendCloseStreamingSession(CancellationToken cancellationToken)
    {
        await TrySend(
            async () =>
            {
                var dto = new CloseStreamingSessionRequestDto();
                var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.CloseStreamingSession, _appState.PrivateKey);
                await Client.Send(signedDto, cancellationToken);
            });
    }
    public async Task SendKeyboardStateReset(CancellationToken cancellationToken)
    {
        await TrySend(
              async () =>
              {
                  var dto = new ResetKeyboardStateDto();
                  var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.ResetKeyboardState, _appState.PrivateKey);
                  await Client.Send(signedDto, cancellationToken);
              });
    }

    public async Task SendKeyEvent(string key, bool isPressed, CancellationToken cancellationToken)
    {
        await TrySend(
              async () =>
              {
                  var dto = new KeyEventDto(key, isPressed);
                  var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.KeyEvent, _appState.PrivateKey);
                  await Client.Send(signedDto, cancellationToken);
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
                var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.MouseButtonEvent, _appState.PrivateKey);
                await Client.Send(signedDto, cancellationToken);
            });
    }

    public async Task SendMouseClick(int button, bool isDoubleClick, double percentX, double percentY, CancellationToken cancellationToken)
    {
        await TrySend(
             async () =>
             {
                 var dto = new MouseClickDto(button, isDoubleClick, percentX, percentY);
                 var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.MouseClick, _appState.PrivateKey);
                 await Client.Send(signedDto, cancellationToken);
             });
    }

    public async Task SendPointerMove(double percentX, double percentY, CancellationToken cancellationToken)
    {
        await TrySend(
            async () =>
            {
                var dto = new MovePointerDto(percentX, percentY);
                var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.MovePointer, _appState.PrivateKey);
                await Client.Send(signedDto, cancellationToken);
            });
    }

    public async Task SendTypeText(string text, CancellationToken cancellationToken)
    {
        await TrySend(
             async () =>
             {
                 var dto = new TypeTextDto(text);
                 var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.TypeText, _appState.PrivateKey);
                 await Client.Send(signedDto, cancellationToken);
             });
    }
    public async Task SendWheelScroll(double percentX, double percentY, double scrollY, double scrollX, CancellationToken cancellationToken)
    {
        await TrySend(
            async () =>
            {
                var dto = new WheelScrollDto(percentX, percentY, scrollY, scrollX);
                var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.WheelScroll, _appState.PrivateKey);
                await Client.Send(signedDto, cancellationToken);
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
            () => Client.State == WebSocketState.Open || _isDisposed,
            TimeSpan.FromSeconds(30),
            pollingMs: 100);
    }
}
