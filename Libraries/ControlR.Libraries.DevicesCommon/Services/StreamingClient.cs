using Bitbound.SimpleMessenger;
using ControlR.Libraries.DevicesCommon.Messages;
using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Buffers;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace ControlR.Libraries.DevicesCommon.Services;

public interface IStreamingClient : IAsyncDisposable, IClosable
{
    WebSocketState State { get; }
    Task Connect(Uri websocketUri, CancellationToken cancellationToken);

    Task Send(SignedPayloadDto dto, CancellationToken cancellationToken);
    Task Send(UnsignedPayloadDto dto, CancellationToken cancellationToken);
    Task WaitForClose(CancellationToken cancellationToken);
}

public sealed class StreamingClient(
    IMessenger _messenger,
    IKeyProvider _keyProvider,
    IMemoryProvider _memoryProvider,
    ILogger<StreamingClient> _logger) : Closable(_logger), IStreamingClient
{
    private readonly ClientWebSocket _client = new();
    private readonly CancellationTokenSource _clientDisposingCts = new();
    private readonly ConcurrentDictionary<Guid, Func<Task>> _onCloseCallbacks = new();
    private readonly SemaphoreSlim _sendLock = new(1);
    private bool _isDisposed;

    public WebSocketState State => _client.State;

    public async Task Connect(Uri websocketUri, CancellationToken cancellationToken)
    {
        await _client.ConnectAsync(websocketUri, cancellationToken);
        ReadFromStream().Forget();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            _clientDisposingCts.Cancel();

            if (_client.State == WebSocketState.Open)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection disposed.", cts.Token);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while closing connection.");
        }
        finally
        {
            _client.Dispose();
        }

        await Close();
    }

    public async Task Send(SignedPayloadDto dto, CancellationToken cancellationToken)
    {
       await SendImpl(dto, true, cancellationToken);
    }

    public async Task Send(UnsignedPayloadDto dto, CancellationToken cancellationToken)
    {
        await SendImpl(dto, false, cancellationToken);
    }

    public async Task WaitForClose(CancellationToken cancellationToken)
    {
        await _clientDisposingCts.Token.WhenCancelled(cancellationToken);
    }

    private async Task ReadFromStream()
    {
        var sizeBuffer = new byte[5];
        var dtoBuffer = new byte[ushort.MaxValue];

        while (_client.State == WebSocketState.Open && !_clientDisposingCts.IsCancellationRequested)
        {
            try
            {
                var result = await _client.ReceiveAsync(sizeBuffer, _clientDisposingCts.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Websocket close message received.");
                    break;
                }

                var bytesRead = result.Count;

                if (bytesRead < 5)
                {
                    _logger.LogError("Failed to get DTO header.");
                    break;
                }

                var isSigned = Convert.ToBoolean(sizeBuffer[0]);
                var size = BitConverter.ToInt32(sizeBuffer, 1);
                using var dtoStream = _memoryProvider.GetRecyclableStream();

                while (dtoStream.Position < size)
                {
                    result = await _client.ReceiveAsync(dtoBuffer, _clientDisposingCts.Token);

                    if (result.MessageType == WebSocketMessageType.Close ||
                        result.Count == 0)
                    {
                        _logger.LogWarning("Stream ended before DTO was complete.");
                        break;
                    }

                    await dtoStream.WriteAsync(dtoBuffer.AsMemory(0, result.Count));
                }

                dtoStream.Seek(0, SeekOrigin.Begin);

                if (isSigned)
                {
                    var dto = await MessagePackSerializer.DeserializeAsync<SignedPayloadDto>(dtoStream, cancellationToken: _clientDisposingCts.Token);
                    if (!_keyProvider.Verify(dto))
                    {
                        return;
                    }

                    var message = new DtoReceivedMessage<SignedPayloadDto>(dto);
                    _messenger.Send(message).Forget();
                }
                else
                {
                    var dto = await MessagePackSerializer.DeserializeAsync<UnsignedPayloadDto>(dtoStream, cancellationToken: _clientDisposingCts.Token);
                    var message = new DtoReceivedMessage<UnsignedPayloadDto>(dto);
                    _messenger.Send(message).Forget();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Streaming cancelled.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while reading from stream.");
                break;
            }
        }

        await DisposeAsync();
    }

    private async Task SendImpl<T>(T dto, bool isSigned, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            var payload = MessagePackSerializer.Serialize(dto, cancellationToken: cancellationToken);
            var size = BitConverter.GetBytes(payload.Length);
            byte[] header = [ Convert.ToByte(isSigned), .. size ];

            await _client.SendAsync(
               header,
               WebSocketMessageType.Binary,
               false,
               cancellationToken);

            await _client.SendAsync(
                payload,
                WebSocketMessageType.Binary,
                true,
                cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }
}
