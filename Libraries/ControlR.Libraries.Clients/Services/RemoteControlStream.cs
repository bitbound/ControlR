using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using ControlR.Libraries.Shared.Collections;
using ControlR.Libraries.Shared.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Services;

namespace ControlR.Libraries.Clients.Services;

public interface IRemoteControlStream
{
  bool IsConnected { get; }
  WebSocketState State { get; }
  Task Close();
  Task Connect(Uri websocketUri, CancellationToken cancellationToken);

  IDisposable OnClosed(Func<Task> callback);
  IDisposable RegisterMessageHandler(object subscriber, Func<DtoWrapper, Task> handler);
  Task Send(DtoWrapper dto, CancellationToken cancellationToken);
  Task WaitForClose(CancellationToken cancellationToken);
}

public abstract class RemoteControlStream(
  TimeProvider timeProvider,
  IMessenger messenger,
  IMemoryProvider memoryProvider,
  IWaiter waiter,
  ILogger<RemoteControlStream> logger) : IRemoteControlStream
{
  private const int MaxSendBufferLength = ushort.MaxValue * 2;

  protected readonly IMessenger Messenger = messenger;
  protected readonly TimeProvider TimeProvider = timeProvider;
  
  private readonly ILogger<RemoteControlStream> _logger = logger;
  private readonly IMemoryProvider _memoryProvider = memoryProvider;
  private readonly ConditionalWeakTable<object, Func<DtoWrapper, Task>> _messageHandlers = [];
  private readonly ConcurrentList<Func<Task>> _onCloseHandlers = [];
  private readonly SemaphoreSlim _sendLock = new(1);
  private readonly IWaiter _waiter = waiter;
  
  private ClientWebSocket? _client;
  private volatile int _sendBufferLength;


  public TimeSpan CurrentLatency { get; private set; }

  public bool IsConnected => State == WebSocketState.Open;
  public WebSocketState State => _client?.State ?? WebSocketState.Closed;


  protected ClientWebSocket Client => _client ?? throw new InvalidOperationException("Client not initialized.");

  protected bool IsDisposed { get; private set; }


  public async Task Close()
  {
    try
    {
      if (State == WebSocketState.Open)
      {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection disposed.", cts.Token);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while closing connection.");
    }
    finally
    {
      IsDisposed = true;
      _client?.Dispose();
      _client = null;
      await InvokeOnClosedHandlers();
    }
  }

  public async Task Connect(Uri websocketUri, CancellationToken cancellationToken)
  {
    _logger.LogInformation("Connecting to {WebsocketUrl}.", websocketUri);
    _client?.Dispose();
    _client = new ClientWebSocket();
    await _client.ConnectAsync(websocketUri, cancellationToken);
    _logger.LogInformation("Connection successful.");
    ReadFromStream(cancellationToken).Forget();
  }

  public IDisposable OnClosed(Func<Task> callback)
  {
    _onCloseHandlers.Add(callback);
    return new CallbackDisposable(() => { _onCloseHandlers.Remove(callback); });
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

    await WaitForSendBuffer(cancellationToken);

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

    await _sendLock.WaitAsync(linkedCts.Token);
    try
    {
      var dtoBytes = MessagePackSerializer.Serialize(dto, cancellationToken: linkedCts.Token);

      await Client.SendAsync(
        dtoBytes,
        WebSocketMessageType.Binary,
        true,
        linkedCts.Token);

      _ = Interlocked.Add(ref _sendBufferLength, dtoBytes.Length);
    }
    finally
    {
      _sendLock.Release();
    }
  }

  public async Task WaitForClose(CancellationToken cancellationToken)
  {
    while (_client?.State == WebSocketState.Open)
    {
      try
      {
        await Task.Delay(100, cancellationToken);
      }
      catch (OperationCanceledException)
      {
        break;
      }
    }
  }


  private async Task<bool> FillStream(MemoryStream dtoStream, byte[] dtoBuffer, CancellationToken cancellationToken)
  {
    while (true)
    {
      var result = await Client.ReceiveAsync(dtoBuffer, cancellationToken);

      if (result.MessageType == WebSocketMessageType.Close)
      {
        return false;
      }

      if (result.Count > 0)
      {
        await dtoStream.WriteAsync(dtoBuffer.AsMemory(0, result.Count), cancellationToken);
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

  private async Task InvokeOnClosedHandlers()
  {
    foreach (var callback in _onCloseHandlers)
    {
      try
      {
        await callback();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while executing on close callback.");
      }
    }
  }

  private async Task ReadFromStream(CancellationToken cancellationToken)
  {
    var dtoBuffer = new byte[ushort.MaxValue];

    while (Client.State == WebSocketState.Open)
    {
      try
      {
        using var dtoStream = _memoryProvider.GetRecyclableStream();
        if (!await FillStream(dtoStream, dtoBuffer, cancellationToken))
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
          cancellationToken: cancellationToken);

        switch (receivedWrapper.DtoType)
        {
          case DtoType.Ack:
          {
            var ackDto = receivedWrapper.GetPayload<AckDto>();
            _ = Interlocked.Add(ref _sendBufferLength, -ackDto.ReceivedSize);
            CurrentLatency = TimeProvider.GetElapsedTime(ackDto.SendTimestamp);
            break;
          }
          default:
          {
            await SendAck(totalBytesRead, receivedWrapper.SendTimestamp, cancellationToken);
            await InvokeMessageHandlers(receivedWrapper, cancellationToken);
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

    await Close();
  }

  private async Task SendAck(int receivedBytes, long sendTimestamp, CancellationToken cancellationToken)
  {
    var ackDto = new AckDto(receivedBytes, sendTimestamp);
    var wrapper = DtoWrapper.Create(ackDto, DtoType.Ack);
    var wrapperBytes = MessagePackSerializer.Serialize(wrapper, cancellationToken: cancellationToken);
    await Client.SendAsync(
      wrapperBytes,
      WebSocketMessageType.Binary,
      true,
      cancellationToken);
  }

  private async Task WaitForSendBuffer(CancellationToken cancellationToken)
  {
    if (_sendBufferLength < MaxSendBufferLength)
    {
      return;
    }

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

    var waitResult = await _waiter.WaitFor(
      () => _sendBufferLength < MaxSendBufferLength,
      TimeSpan.FromMilliseconds(25),
      cancellationToken: linkedCts.Token);

    if (!waitResult)
    {
      _logger.LogError("Timed out while waiting for send buffer to drain.");
    }
  }
}