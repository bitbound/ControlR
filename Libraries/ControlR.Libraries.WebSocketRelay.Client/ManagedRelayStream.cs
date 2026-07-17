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
  IStreamMetrics streamMetrics,
  ILogger<ManagedRelayStream> logger) : IManagedRelayStream
{
  private const int MaxSendBufferLength = ushort.MaxValue * 2;
  private static readonly TimeSpan _closeTimeout = TimeSpan.FromSeconds(1);

  protected readonly IMessenger Messenger = messenger;
  protected readonly TimeProvider TimeProvider = timeProvider;

  private readonly ILogger<ManagedRelayStream> _logger = logger;
  private readonly IMemoryProvider _memoryProvider = memoryProvider;
  private readonly HandlerCollection<DtoWrapper> _messageHandlers = new(ex => { logger.LogError(ex, "Error while invoking message handler."); return Task.CompletedTask; });
  private readonly ConcurrentList<Func<Task>> _onCloseHandlers = [];
  private readonly SemaphoreSlim _sendLock = new(1);
  private readonly IStreamMetrics _streamMetrics = streamMetrics;
  private readonly IWaiter _waiter = waiter;

  private ActiveConnection? _connection;
  private int _isDisposed;
  private int _sendBufferLength;

  public TimeSpan CurrentLatency => _streamMetrics.GetCurrentLatency();
  public bool IsConnected => State == WebSocketState.Open;
  public WebSocketState State => _connection?.Client.State ?? WebSocketState.Closed;

  protected bool IsDisposed => _isDisposed == 1;

  public async Task<Result> Close()
  {
    var conn = Interlocked.Exchange(ref _connection, null);

    if (conn is null)
    {
      return Result.Ok();
    }

    return await TearDown(conn);
  }

  public Task Connect(Uri websocketUri, CancellationToken cancellationToken) =>
    Connect(websocketUri, _ => { }, cancellationToken);

  public async Task Connect(Uri websocketUri, Action<ClientWebSocketOptions> configureOptions, CancellationToken cancellationToken)
  {
    await Close();
    ObjectDisposedException.ThrowIf(IsDisposed, this);

    _logger.LogInformation("Connecting to {WebSocketOrigin} with custom options.", $"{websocketUri.Scheme}://{websocketUri.Authority}");

    var client = new ClientWebSocket();
    CancellationTokenSource? readCancellation = null;

    try
    {
      configureOptions(client.Options);
      await client.ConnectAsync(websocketUri, cancellationToken);
      readCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

      var conn = new ActiveConnection(client, readCancellation);
      Interlocked.Exchange(ref _connection, conn);

      if (IsDisposed)
      {
        if (Interlocked.CompareExchange(ref _connection, null, conn) == conn)
        {
          await TearDown(conn);
        }

        throw new ObjectDisposedException(GetType().FullName);
      }

      _logger.LogInformation("Connection successful.");

      _ = ReadFromStream(conn);
    }
    catch
    {
      readCancellation?.Dispose();
      client.Dispose();
      throw;
    }
  }

  public async ValueTask DisposeAsync()
  {
    await DisposeAsync(true);
    GC.SuppressFinalize(this);
  }

  public double GetMbpsIn() => _streamMetrics.GetMbpsIn();

  public double GetMbpsOut() => _streamMetrics.GetMbpsOut();

  public IDisposable OnClosed(Func<Task> callback)
  {
    _onCloseHandlers.Add(callback);
    return new CallbackDisposable(() => { _onCloseHandlers.Remove(callback); });
  }

  public IDisposable RegisterMessageHandler(object subscriber, Func<DtoWrapper, Task> handler) =>
    _messageHandlers.AddHandler(subscriber, handler);

  public async Task Send(DtoWrapper dto, CancellationToken cancellationToken)
  {
    var conn = _connection ?? throw new InvalidOperationException("Client has not been initialized.");

    if (dto.DtoType == DtoType.Ack)
    {
      throw new ArgumentException("Cannot send ACK DTOs with this method.  Use SendAck instead.");
    }

    await WaitForSendBuffer(cancellationToken);

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
    using var sendLocker = await _sendLock.AcquireLockAsync(cancellationToken: linkedCts.Token);

    var dtoBytes = MessagePackSerializer.Serialize(dto, cancellationToken: linkedCts.Token);
    await conn.Client.SendAsync(
      dtoBytes,
      WebSocketMessageType.Binary,
      true,
      linkedCts.Token);

    _ = Interlocked.Add(ref _sendBufferLength, dtoBytes.Length);
    _streamMetrics.RecordBytesOut(new TransferRecord(dtoBytes.Length, TimeProvider.GetTimestamp()));
  }

  public Task WaitForClose(CancellationToken cancellationToken) =>
    _waiter.WaitFor(() => _connection?.Client.State != WebSocketState.Open, cancellationToken: cancellationToken);

  protected virtual async ValueTask DisposeAsync(bool disposing)
  {
    if (!disposing)
    {
      return;
    }

    if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
    {
      return;
    }

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
      _onCloseHandlers.Clear();
    }
  }

  private async Task<Result> CloseSocket(ClientWebSocket client)
  {
    try
    {
      if (client.State == WebSocketState.Open)
      {
        using var closeCts = new CancellationTokenSource(_closeTimeout);
        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed.", closeCts.Token);
      }
      return Result.Ok();
    }
    catch (OperationCanceledException ex)
    {
      if (client.State == WebSocketState.Open)
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

  private async Task InvokeOnClosedHandlers(ActiveConnection conn)
  {
    if (!conn.TryClaimCloseHandlers())
    {
      return;
    }

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

  private async Task ReadFromStream(ActiveConnection conn)
  {
    try
    {
      while (conn.Client.State == WebSocketState.Open)
      {
        try
        {
          using var dtoStream = _memoryProvider.GetRecyclableStream();
          using var webSocketStream = WebSocketStream.CreateReadableMessageStream(conn.Client);
          await webSocketStream.CopyToAsync(dtoStream, conn.ReadCancellation.Token);

          var totalBytesRead = (int)dtoStream.Length;

          if (totalBytesRead == 0)
          {
            _logger.LogWarning("Received empty message.");
            continue;
          }

          _streamMetrics.RecordBytesIn(new TransferRecord(totalBytesRead, TimeProvider.GetTimestamp()));

          dtoStream.Seek(0, SeekOrigin.Begin);

          var receivedWrapper = await MessagePackSerializer.DeserializeAsync<DtoWrapper>(
            dtoStream,
            cancellationToken: conn.ReadCancellation.Token);

          switch (receivedWrapper.DtoType)
          {
            case DtoType.Ack:
              {
                var ackDto = receivedWrapper.GetPayload<AckDto>();
                _ = Interlocked.Add(ref _sendBufferLength, -ackDto.ReceivedSize);
                _streamMetrics.SetCurrentLatency(TimeProvider.GetElapsedTime(ackDto.SendTimestamp));
                break;
              }
            default:
              {
                await SendAck(conn.Client, totalBytesRead, receivedWrapper.SendTimestamp, conn.ReadCancellation.Token);
                await _messageHandlers.InvokeHandlers(receivedWrapper, conn.ReadCancellation.Token);
                break;
              }
          }
        }
        catch (OperationCanceledException)
        {
          _logger.LogInformation("Streaming cancelled.");
          break;
        }
        catch (ObjectDisposedException)
        {
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
    }
    finally
    {
      if (Interlocked.CompareExchange(ref _connection, null, conn) == conn)
      {
        await TearDown(conn);
      }
    }
  }

  private async Task SendAck(ClientWebSocket client, int receivedBytes, long sendTimestamp, CancellationToken cancellationToken)
  {
    var ackDto = new AckDto(receivedBytes, sendTimestamp);
    var wrapper = DtoWrapper.Create(ackDto, DtoType.Ack);
    var wrapperBytes = MessagePackSerializer.Serialize(wrapper, cancellationToken: cancellationToken);
    _streamMetrics.RecordBytesOut(new TransferRecord(wrapperBytes.Length, TimeProvider.GetTimestamp()));
    await client.SendAsync(
      wrapperBytes,
      WebSocketMessageType.Binary,
      true,
      cancellationToken);
  }

  private async Task<Result> TearDown(ActiveConnection conn)
  {
    conn.ReadCancellation.Cancel();

    var result = await CloseSocket(conn.Client);
    conn.Client.Dispose();
    conn.ReadCancellation.Dispose();

    Interlocked.Exchange(ref _sendBufferLength, 0);
    await InvokeOnClosedHandlers(conn);

    return result;
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

  private sealed class ActiveConnection(ClientWebSocket client, CancellationTokenSource readCancellation)
  {
    private int _closeHandlersInvoked;

    public ClientWebSocket Client { get; } = client;
    public CancellationTokenSource ReadCancellation { get; } = readCancellation;

    public bool TryClaimCloseHandlers() =>
      Interlocked.Exchange(ref _closeHandlersInvoked, 1) == 0;
  }
}
