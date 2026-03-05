using System.Collections.Concurrent;
using System.Net.WebSockets;
using Bitbound.SimpleMessenger;
using ControlR.Libraries.Shared.Collections;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Buffers;
using MessagePack;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.WebSocketRelay.Client;

public interface IManagedRelayStream : IAsyncDisposable
{
  TimeSpan CurrentLatency { get; }
  bool IsConnected { get; }
  WebSocketState State { get; }
  Task<Result> Close();
  Task Connect(Uri websocketUri, CancellationToken cancellationToken);
  Task Connect(Uri websocketUri, Action<ClientWebSocketOptions> configureOptions, CancellationToken cancellationToken);

  double GetMbpsIn();
  double GetMbpsOut();
  IDisposable OnClosed(Func<Task> callback);
  IDisposable RegisterMessageHandler(object subscriber, Func<DtoWrapper, Task> handler);
  Task Send(DtoWrapper dto, CancellationToken cancellationToken);
  Task WaitForClose(CancellationToken cancellationToken);
}

public abstract class ManagedRelayStream(
  TimeProvider timeProvider,
  IMessenger messenger,
  IMemoryProvider memoryProvider,
  IWaiter waiter,
  ILogger<ManagedRelayStream> logger) : IManagedRelayStream
{
  private const double MaxQueueAgeSeconds = 3;
  private const int MaxSendBufferLength = ushort.MaxValue * 2;

  protected readonly IMessenger Messenger = messenger;
  protected readonly TimeProvider TimeProvider = timeProvider;

  private readonly ConcurrentQueue<TransferRecord> _bytesIn = new();
  private readonly ConcurrentQueue<TransferRecord> _bytesOut = new();
  private readonly SemaphoreSlim _disposeLock = new(1, 1);
  private readonly ILogger<ManagedRelayStream> _logger = logger;
  private readonly IMemoryProvider _memoryProvider = memoryProvider;
  private readonly HandlerCollection<DtoWrapper> _messageHandlers = new(ex => { logger.LogError(ex, "Error while invoking message handler."); return Task.CompletedTask; });
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

  public async Task<Result> Close()
  {
    try
    {
      if (Client.State == WebSocketState.Open)
      {
        using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed.", closeCts.Token);
      }
      await InvokeOnClosedHandlers();
      return Result.Ok();
    }
    catch (OperationCanceledException ex)
    {
      if (Client.State == WebSocketState.Open)
      {
        const string message = "Timed out while closing connection.";
        _logger.LogWarning(ex, message);
        return Result.Fail(ex, message);
      }
      return Result.Ok();
    }
    catch (WebSocketException ex) when (ex.WebSocketErrorCode is WebSocketError.InvalidState or WebSocketError.ConnectionClosedPrematurely)
    {
      return Result.Ok();
    }
    catch (Exception ex)
    {
      const string message = "Error while closing connection.";
      _logger.LogWarning(ex, message);
      return Result.Fail(ex, message);
    }
  }

  public async Task Connect(Uri websocketUri, CancellationToken cancellationToken)
  {
    _logger.LogInformation("Connecting to {WebSocketOrigin}.", $"{websocketUri.Scheme}://{websocketUri.Authority}");
    _client?.Dispose();
    _client = new ClientWebSocket();
    await _client.ConnectAsync(websocketUri, cancellationToken);
    _logger.LogInformation("Connection successful.");
    ReadFromStream(cancellationToken).Forget();
  }

  public async Task Connect(Uri websocketUri, Action<ClientWebSocketOptions> configureOptions, CancellationToken cancellationToken)
  {
    _logger.LogInformation("Connecting to {WebSocketOrigin} with custom options.", $"{websocketUri.Scheme}://{websocketUri.Authority}");
    _client?.Dispose();
    _client = new ClientWebSocket();
    configureOptions(_client.Options);

    await _client.ConnectAsync(websocketUri, cancellationToken);
    _logger.LogInformation("Connection successful.");
    ReadFromStream(cancellationToken).Forget();
  }

  public async ValueTask DisposeAsync()
  {
    await DisposeAsync(true);
    GC.SuppressFinalize(this);
  }

  public double GetMbpsIn()
  {
    CleanupQueue(_bytesIn);
    var totalBytes = _bytesIn.Sum(x => x.Size);
    return ToMegabits(totalBytes);
  }

  public double GetMbpsOut()
  {
    CleanupQueue(_bytesOut);
    var totalBytes = _bytesOut.Sum(x => x.Size);
    return ToMegabits(totalBytes);
  }

  public IDisposable OnClosed(Func<Task> callback)
  {
    _onCloseHandlers.Add(callback);
    return new CallbackDisposable(() => { _onCloseHandlers.Remove(callback); });
  }

  public IDisposable RegisterMessageHandler(object subscriber, Func<DtoWrapper, Task> handler)
  {
    return _messageHandlers.AddHandler(subscriber, handler);
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

    using var sendLocker = await _sendLock.AcquireLockAsync(cancellationToken: linkedCts.Token);

    var dtoBytes = MessagePackSerializer.Serialize(dto, cancellationToken: linkedCts.Token);

    await Client.SendAsync(
      dtoBytes,
      WebSocketMessageType.Binary,
      true,
      linkedCts.Token);

    _ = Interlocked.Add(ref _sendBufferLength, dtoBytes.Length);
    _bytesOut.Enqueue(new TransferRecord(dtoBytes.Length, TimeProvider.GetTimestamp()));
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

  protected virtual async ValueTask DisposeAsync(bool disposing)
  {
    if (!disposing)
    {
      return;
    }

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    using var locker = await _disposeLock.AcquireLockAsync(throwOnTimeout: false, cts.Token);

    if (locker is null)
    {
      _logger.LogWarning("Timed out while waiting to acquire dispose lock. Dispose may already be in progress.");
    }

    ObjectDisposedException.ThrowIf(IsDisposed, this);

    try
    {
      await Close();
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Error while closing connection.");
    }
    finally
    {
      IsDisposed = true;
      _client?.Dispose();
      _client = null;
      _onCloseHandlers.Clear();
    }
  }

  private static double ToMegabits(int totalBytes)
  {
    var totalBits = totalBytes * 8;
    var megabits = totalBits / 1_000_000.0;
    return megabits / MaxQueueAgeSeconds;
  }

  private void CleanupQueue(ConcurrentQueue<TransferRecord> queue)
  {
    var cutoffTime = TimeProvider.GetTimestamp() - TimeProvider.TimestampFrequency * (long)MaxQueueAgeSeconds;

    while (queue.TryPeek(out var record))
    {
      if (record.Timestamp < cutoffTime)
      {
        queue.TryDequeue(out _);
      }
      else
      {
        break;
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
    while (Client.State == WebSocketState.Open)
    {
      try
      {
        using var dtoStream = _memoryProvider.GetRecyclableStream();
        using var webSocketStream = WebSocketStream.CreateReadableMessageStream(Client);
        await webSocketStream.CopyToAsync(dtoStream, cancellationToken);

        var totalBytesRead = (int)dtoStream.Length;

        if (totalBytesRead == 0)
        {
          _logger.LogWarning("Received empty message.");
          continue;
        }

        _bytesIn.Enqueue(new TransferRecord(totalBytesRead, TimeProvider.GetTimestamp()));
        
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
              await _messageHandlers.InvokeHandlers(receivedWrapper, cancellationToken);
              break;
            }
        }
      }
      catch (OperationCanceledException)
      {
        _logger.LogInformation("Streaming cancelled.");
        break;
      }
      catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.InvalidState)
      {
        _logger.LogWarning(ex, "WebSocket is in an invalid state. Ending read loop.");
        break;
      }
      catch (MessagePackSerializationException ex) when (ex.InnerException is EndOfStreamException)
      {
        _logger.LogInformation("End of stream.");
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
    _bytesOut.Enqueue(new TransferRecord(wrapperBytes.Length, TimeProvider.GetTimestamp()));
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

  private record TransferRecord(int Size, long Timestamp);
}