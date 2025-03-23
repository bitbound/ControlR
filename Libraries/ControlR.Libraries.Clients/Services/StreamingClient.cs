using ControlR.Libraries.Shared.Services;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ControlR.Libraries.Clients.Services;

public interface IStreamingClient : IAsyncDisposable, IClosable
{
  WebSocketState State { get; }
  Task Connect(Uri websocketUri, CancellationToken cancellationToken);
  IDisposable RegisterMessageHandler(object subscriber, Func<DtoWrapper, Task> handler);
  Task Send(DtoWrapper dto, CancellationToken cancellationToken);
  Task WaitForClose(CancellationToken cancellationToken);
}

public abstract class StreamingClient(
  IMessenger messenger,
  IMemoryProvider memoryProvider,
  IDelayer delayer,
  ILogger<StreamingClient> logger) : Closable(logger), IStreamingClient
{
  protected readonly IMessenger Messenger = messenger;
  private readonly int _maxSendBufferLength = ushort.MaxValue * 2;
  private readonly CancellationTokenSource _clientDisposingCts = new();
  private readonly IDelayer _delayer = delayer;
  private readonly ILogger<StreamingClient> _logger = logger;
  private readonly IMemoryProvider _memoryProvider = memoryProvider;
  private readonly Guid _messageDelimiter = Guid.Parse("84da960a-54ec-47f5-a8b5-fa362221e8bf");
  private readonly ConditionalWeakTable<object, Func<DtoWrapper, Task>> _messageHandlers = [];
  private readonly SemaphoreSlim _sendLock = new(1);
  private ClientWebSocket? _client;
  private volatile int _sendBufferLength;

  private enum MessageType : short
  {
    Dto,
    Ack
  }

  public WebSocketState State => _client?.State ?? WebSocketState.Closed;

  protected ClientWebSocket Client => _client ?? throw new Exception("Client not initialized.");
  protected bool IsDisposed { get; private set; }

  public async Task Connect(Uri websocketUri, CancellationToken cancellationToken)
  {
    _client?.Dispose();
    _client = new ClientWebSocket();
    await _client.ConnectAsync(websocketUri, cancellationToken);
    ReadFromStream().Forget();
  }

  public async ValueTask DisposeAsync()
  {
    try
    {
      if (IsDisposed)
      {
        return;
      }

      IsDisposed = true;

      await _clientDisposingCts.CancelAsync();

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

  public IDisposable RegisterMessageHandler(object subscriber, Func<DtoWrapper, Task> handler)
  {
    lock (_messageHandlers)
    {
      _messageHandlers.AddOrUpdate(subscriber, handler);
    }
    
    return new CallbackDisposable(() =>
    {
      lock (_messageHandlers)
      {
        _messageHandlers.Remove(subscriber);
      }
    });
  }

  public async Task Send(DtoWrapper dto, CancellationToken cancellationToken)
  {
    if (!await WaitForSendBuffer(cancellationToken))
    {
      return;
    }

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
    await SendDto(dto, linkedCts.Token);
  }

  private async Task<bool> WaitForSendBuffer(CancellationToken cancellationToken)
  {
    if (_sendBufferLength < _maxSendBufferLength)
    {
      return true;
    }

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

    var waitResult = await _delayer.WaitForAsync(
        () => _sendBufferLength < _maxSendBufferLength,
        pollingDelay: TimeSpan.FromMilliseconds(25),
        cancellationToken: linkedCts.Token);

    if (waitResult)
    {
      return true;
    }

    _logger.LogError("Timed out while waiting for send buffer to drain.");
    await DisposeAsync();
    return false;
  }

  public async Task WaitForClose(CancellationToken cancellationToken)
  {
    await _clientDisposingCts.Token.WhenCancelled(cancellationToken);
  }

  private static MessageHeader GetHeader(byte[] buffer)
  {
    return new MessageHeader(
      new Guid(buffer[..16]),
      (MessageType)BitConverter.ToInt16(buffer.AsSpan()[16..18]),
      BitConverter.ToInt32(buffer.AsSpan()[18..22]));
  }

  private static byte[] GetHeaderBytes(MessageHeader header)
  {
    return
    [
      .. header.Delimiter.ToByteArray(),
      .. BitConverter.GetBytes((short)header.MessageType),
      .. BitConverter.GetBytes(header.MessageSize)
    ];
  }

  private List<Func<DtoWrapper, Task>> GetMessageHandlers()
  {
    lock (_messageHandlers)
    {
      return [.. _messageHandlers.Select(x => x.Value)];
    }
  }

  private void HandleAck(MessageHeader header)
  {
    _ = Interlocked.Add(ref _sendBufferLength, -header.MessageSize);
  }

  private async Task HandleDtoMessage(MessageHeader header, byte[] dtoBuffer)
  {
    using var dtoStream = _memoryProvider.GetRecyclableStream();

    while (dtoStream.Position < header.MessageSize)
    {
      var result = await Client.ReceiveAsync(dtoBuffer, _clientDisposingCts.Token);

      if (result.MessageType == WebSocketMessageType.Close ||
          result.Count == 0)
      {
        _logger.LogWarning("Stream ended before DTO was complete.");
        break;
      }

      await dtoStream.WriteAsync(dtoBuffer.AsMemory(0, result.Count));
      await SendAck(result.Count);
    }

    dtoStream.Seek(0, SeekOrigin.Begin);

    var dto = await MessagePackSerializer.DeserializeAsync<DtoWrapper>(dtoStream,
      cancellationToken: _clientDisposingCts.Token);
    await InvokeMessageHandlers(dto, _clientDisposingCts.Token);

  }

  private async Task InvokeMessageHandlers(DtoWrapper dto, CancellationToken cancellationToken)
  {
    var handlers = GetMessageHandlers();

    foreach (var handler in handlers)
    {
      try
      {
        if (cancellationToken.IsCancellationRequested)
        {
          break;
        }
        
        await handler(dto);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while invoking message handler.");
      }
    }
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

        switch (header.MessageType)
        {
          case MessageType.Dto:
            await SendAck(bytesRead);
            await HandleDtoMessage(header, dtoBuffer);
            break;
          case MessageType.Ack:
            HandleAck(header);
            break;
          default:
            throw new InvalidOperationException($"Unknown message type: {header.MessageType}");
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
  private async Task SendAck(int receivedBytes)
  {
    await _sendLock.WaitAsync(_clientDisposingCts.Token);
    try
    {
      var header = new MessageHeader(_messageDelimiter, MessageType.Ack, receivedBytes);
      var headerBytes = GetHeaderBytes(header);
      await Client.SendAsync(
        headerBytes,
        WebSocketMessageType.Binary,
        true,
        _clientDisposingCts.Token);
    }
    finally
    {
      _sendLock.Release();
    }
  }

  private async Task SendDto<T>(T dto, CancellationToken cancellationToken)
  {
    await _sendLock.WaitAsync(cancellationToken);
    try
    {
      var payload = MessagePackSerializer.Serialize(dto, cancellationToken: cancellationToken);
      var header = new MessageHeader(_messageDelimiter, MessageType.Dto, payload.Length);
      var headerBytes = GetHeaderBytes(header);

      await Client.SendAsync(
        headerBytes,
        WebSocketMessageType.Binary,
        false,
        cancellationToken);

      await Client.SendAsync(
        payload,
        WebSocketMessageType.Binary,
        true,
        cancellationToken);

      _ = Interlocked.Add(ref _sendBufferLength, headerBytes.Length + payload.Length);
    }
    finally
    {
      _sendLock.Release();
    }
  }

  [StructLayout(LayoutKind.Explicit)]
  private struct MessageHeader(Guid delimiter, MessageType messageType, int messageSize)
  {
    public const int Size = 22;

    [FieldOffset(0)]
    public readonly Guid Delimiter = delimiter;

    [FieldOffset(16)]
    public MessageType MessageType = messageType;

    /// <summary>
    /// <para>
    ///   For Dto message type, this will be the message size following the header.
    /// </para>
    /// <para>
    ///   For Ack message type, this will be the number of bytes received by the other client.
    /// </para>
    /// </summary>
    [FieldOffset(18)]
    public readonly int MessageSize = messageSize;
  }
}