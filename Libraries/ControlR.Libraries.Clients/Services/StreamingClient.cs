using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Services;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;

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
  private readonly CancellationTokenSource _clientDisposingCts = new();
  private readonly IDelayer _delayer = delayer;
  private readonly ILogger<StreamingClient> _logger = logger;
  private readonly int _maxSendBufferLength = ushort.MaxValue * 2;
  private readonly IMemoryProvider _memoryProvider = memoryProvider;
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
    _logger.LogInformation("Connecting to {WebsocketUrl}.", websocketUri);
    _client?.Dispose();
    _client = new ClientWebSocket();
    await _client.ConnectAsync(websocketUri, cancellationToken);
    _logger.LogInformation("Connection successful.");
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
    if (dto.DtoType == DtoType.Ack)
    {
      throw new ArgumentException("Cannot send ACK DTOs with this method.  Use SendAck instead.");
    }

    if (!await WaitForSendBuffer(cancellationToken))
    {
      return;
    }

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

    await _sendLock.WaitAsync(linkedCts.Token);
    try
    {
      var dtoBytes = MessagePackSerializer.Serialize(dto, cancellationToken: cancellationToken);

      await Client.SendAsync(
          dtoBytes,
          WebSocketMessageType.Binary,
          true,
          cancellationToken);

      _ = Interlocked.Add(ref _sendBufferLength, dtoBytes.Length);
    }
    finally
    {
      _sendLock.Release();
    }
  }

  public async Task WaitForClose(CancellationToken cancellationToken)
  {
    await _clientDisposingCts.Token.WhenCancelled(cancellationToken);
  }

  private async Task<bool> FillStream(MemoryStream dtoStream, byte[] dtoBuffer, CancellationToken token)
  {
    while (true)
    {
      var result = await Client.ReceiveAsync(dtoBuffer, token);

      if (result.MessageType == WebSocketMessageType.Close)
      {
        return false;
      }

      if (result.Count > 0)
      {
        await dtoStream.WriteAsync(dtoBuffer.AsMemory(0, result.Count), token);
      }

      if (result.EndOfMessage)
      {
        return true;
      }
    }
  }

  private List<Func<DtoWrapper, Task>> GetMessageHandlers()
  {
    lock (_messageHandlers)
    {
      return [.. _messageHandlers.Select(x => x.Value)];
    }
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
    var dtoBuffer = new byte[ushort.MaxValue];

    while (Client.State == WebSocketState.Open && !_clientDisposingCts.IsCancellationRequested)
    {
      try
      {
        using var dtoStream = _memoryProvider.GetRecyclableStream();
        if (!await FillStream(dtoStream, dtoBuffer, _clientDisposingCts.Token))
        {
          break;
        }

        var totalBytesRead = (int)dtoStream.Length;

        if (totalBytesRead == 0)
        {
          _logger.LogWarning("Received empty message.");
          continue;
        }

        dtoStream.Seek(0, SeekOrigin.Begin);
        var receivedWrapper = await MessagePackSerializer.DeserializeAsync<DtoWrapper>(
          dtoStream,
          cancellationToken: _clientDisposingCts.Token);

        switch (receivedWrapper.DtoType)
        {
          case DtoType.Ack:
            {
              var ackDto = receivedWrapper.GetPayload<AckDto>();
              _ = Interlocked.Add(ref _sendBufferLength, -ackDto.ReceivedSize);
              break;
            }
          default:
            {
              await SendAck(totalBytesRead);
              await InvokeMessageHandlers(receivedWrapper, _clientDisposingCts.Token);
              break;
            }
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
    var ackDto = new AckDto(receivedBytes);
    var wrapper = DtoWrapper.Create(ackDto, DtoType.Ack);
    var wrapperBytes = MessagePackSerializer.Serialize(wrapper);
    await Client.SendAsync(
      wrapperBytes,
      WebSocketMessageType.Binary,
      true,
      _clientDisposingCts.Token);
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
}