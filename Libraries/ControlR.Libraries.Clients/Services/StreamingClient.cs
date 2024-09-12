using Bitbound.SimpleMessenger;
using ControlR.Libraries.DevicesCommon.Messages;
using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Buffers;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Runtime.InteropServices;

namespace ControlR.Libraries.Clients.Services;

public interface IStreamingClient : IAsyncDisposable, IClosable
{
    WebSocketState State { get; }
    Task Connect(Uri websocketUri, CancellationToken cancellationToken);

    Task Send(SignedPayloadDto dto, CancellationToken cancellationToken);
    Task Send(UnsignedPayloadDto dto, CancellationToken cancellationToken);
    Task WaitForClose(CancellationToken cancellationToken);
}

public abstract class StreamingClient(
    IKeyProvider keyProvider,
    IMessenger messenger,
    IMemoryProvider _memoryProvider,
    ILogger<StreamingClient> _logger) : Closable(_logger), IStreamingClient
{
    protected readonly IKeyProvider _keyProvider = keyProvider;
    protected readonly IMessenger _messenger = messenger;

    private readonly CancellationTokenSource _clientDisposingCts = new();
    private readonly Guid _messageDelimiter = Guid.Parse("84da960a-54ec-47f5-a8b5-fa362221e8bf");
    private readonly SemaphoreSlim _sendLock = new(1);
    private ClientWebSocket? _client;
    private bool _isDisposed;

    public WebSocketState State => _client?.State ?? WebSocketState.Closed;

    protected ClientWebSocket Client => _client ?? throw new Exception("Client not initialized.");
    protected bool IsDisposed => _isDisposed;

    public async Task Connect(Uri websocketUri, CancellationToken cancellationToken)
    {
        _client?.Dispose();
        _client = new();
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

            if (State == WebSocketState.Open)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await Client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection disposed.", cts.Token);
            }

            GC.SuppressFinalize(this);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while closing connection.");
        }
        finally
        {
            Client.Dispose();
        }
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

    private static MessageHeader GetHeader(byte[] buffer)
    {
        return new MessageHeader(
            new Guid(buffer[..16]),
            Convert.ToBoolean(buffer[16]),
            BitConverter.ToInt32(buffer.AsSpan()[17..21]));
    }

    private static byte[] GetHeaderBytes(MessageHeader header)
    {
        return [
            .. header.Delimiter.ToByteArray(),
            Convert.ToByte(header.IsSigned),
            .. BitConverter.GetBytes(header.DtoSize)
        ];
    }

    private async Task ReadFromStream()
    {
        var headerBuffer = new byte[MessageHeader.Size];
        var dtoBuffer = new byte[ushort.MaxValue];

        while (Client.State == WebSocketState.Open && !_clientDisposingCts.IsCancellationRequested)
        {
            try
            {
                var result = await Client.ReceiveAsync(headerBuffer, _clientDisposingCts.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Websocket close message received.");
                    break;
                }

                var bytesRead = result.Count;

                if (bytesRead < MessageHeader.Size)
                {
                    _logger.LogError("Failed to get DTO header.");
                    break;
                }

                var header = GetHeader(headerBuffer);

                if (header.Delimiter != _messageDelimiter)
                {
                    _logger.LogCritical("Message header delimiter was incorrect.  Value: {Delimiter}", header.Delimiter);
                    break;
                }

                using var dtoStream = _memoryProvider.GetRecyclableStream();

                while (dtoStream.Position < header.DtoSize)
                {
                    result = await Client.ReceiveAsync(dtoBuffer, _clientDisposingCts.Token);

                    if (result.MessageType == WebSocketMessageType.Close ||
                        result.Count == 0)
                    {
                        _logger.LogWarning("Stream ended before DTO was complete.");
                        break;
                    }

                    await dtoStream.WriteAsync(dtoBuffer.AsMemory(0, result.Count));
                }

                dtoStream.Seek(0, SeekOrigin.Begin);

                if (header.IsSigned)
                {
                    var dto = await MessagePackSerializer.DeserializeAsync<SignedPayloadDto>(dtoStream, cancellationToken: _clientDisposingCts.Token);
                    if (!_keyProvider.Verify(dto))
                    {
                        return;
                    }

                    var message = new DtoReceivedMessage<SignedPayloadDto>(dto);
                    await _messenger.Send(message);
                }
                else
                {
                    var dto = await MessagePackSerializer.DeserializeAsync<UnsignedPayloadDto>(dtoStream, cancellationToken: _clientDisposingCts.Token);
                    var message = new DtoReceivedMessage<UnsignedPayloadDto>(dto);
                    await _messenger.Send(message);
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

        await InvokeOnClosed();
    }

    private async Task SendImpl<T>(T dto, bool isSigned, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            var payload = MessagePackSerializer.Serialize(dto, cancellationToken: cancellationToken);
            var header = new MessageHeader(_messageDelimiter, isSigned, payload.Length);


            await Client.SendAsync(
               GetHeaderBytes(header),
               WebSocketMessageType.Binary,
               false,
               cancellationToken);

            await Client.SendAsync(
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

    [StructLayout(LayoutKind.Explicit)]
    private struct MessageHeader(Guid _delimiter, bool _isSigned, int _messageSize)
    {
        public static int Size = 21;

        [FieldOffset(0)]
        public readonly Guid Delimiter = _delimiter;

        [FieldOffset(16)]
        public readonly bool IsSigned = _isSigned;

        [FieldOffset(17)]
        public readonly int DtoSize = _messageSize;
    }
}
