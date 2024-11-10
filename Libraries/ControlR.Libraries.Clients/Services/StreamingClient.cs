using System.Collections.Immutable;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ControlR.Libraries.Shared.Dtos.HubDtos;

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
  ILogger<StreamingClient> logger) : Closable(logger), IStreamingClient
{
  private readonly CancellationTokenSource _clientDisposingCts = new();
  private readonly Guid _messageDelimiter = Guid.Parse("84da960a-54ec-47f5-a8b5-fa362221e8bf");
  private readonly ConditionalWeakTable<object, Func<DtoWrapper, Task>> _messageHandlers = new();
  private readonly SemaphoreSlim _sendLock = new(1);
  protected readonly IMessenger Messenger = messenger;
  private ClientWebSocket? _client;

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
      logger.LogError(ex, "Error while closing connection.");
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
    await SendImpl(dto, cancellationToken);
  }

  public async Task WaitForClose(CancellationToken cancellationToken)
  {
    await _clientDisposingCts.Token.WhenCancelled(cancellationToken);
  }

  private static MessageHeader GetHeader(byte[] buffer)
  {
    return new MessageHeader(
      new Guid(buffer[..16]),
      BitConverter.ToInt32(buffer.AsSpan()[16..20]));
  }

  private static byte[] GetHeaderBytes(MessageHeader header)
  {
    return
    [
      .. header.Delimiter.ToByteArray(),
      .. BitConverter.GetBytes(header.DtoSize)
    ];
  }

  private ImmutableList<Func<DtoWrapper, Task>> GetMessageHandlers()
  {
    lock (_messageHandlers)
    {
      return _messageHandlers.Select(x => x.Value).ToImmutableList();
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
        logger.LogError(ex, "Error while invoking message handler.");
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
          logger.LogInformation("Websocket close message received.");
          break;
        }

        var bytesRead = result.Count;

        if (bytesRead < MessageHeader.Size)
        {
          logger.LogError("Failed to get DTO header.");
          break;
        }

        var header = GetHeader(headerBuffer);

        if (header.Delimiter != _messageDelimiter)
        {
          logger.LogCritical("Message header delimiter was incorrect.  Value: {Delimiter}", header.Delimiter);
          break;
        }

        using var dtoStream = memoryProvider.GetRecyclableStream();

        while (dtoStream.Position < header.DtoSize)
        {
          result = await Client.ReceiveAsync(dtoBuffer, _clientDisposingCts.Token);

          if (result.MessageType == WebSocketMessageType.Close ||
              result.Count == 0)
          {
            logger.LogWarning("Stream ended before DTO was complete.");
            break;
          }

          await dtoStream.WriteAsync(dtoBuffer.AsMemory(0, result.Count));
        }

        dtoStream.Seek(0, SeekOrigin.Begin);

        var dto = await MessagePackSerializer.DeserializeAsync<DtoWrapper>(dtoStream,
          cancellationToken: _clientDisposingCts.Token);
        await InvokeMessageHandlers(dto, _clientDisposingCts.Token);
      }
      catch (OperationCanceledException)
      {
        logger.LogInformation("Streaming cancelled.");
        break;
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error while reading from stream.");
        break;
      }
    }

    await InvokeOnClosed();
  }

  private async Task SendImpl<T>(T dto, CancellationToken cancellationToken)
  {
    await _sendLock.WaitAsync(cancellationToken);
    try
    {
      var payload = MessagePackSerializer.Serialize(dto, cancellationToken: cancellationToken);
      var header = new MessageHeader(_messageDelimiter, payload.Length);


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
  private struct MessageHeader(Guid delimiter, int messageSize)
  {
    public const int Size = 20;

    [FieldOffset(0)] public readonly Guid Delimiter = delimiter;

    [FieldOffset(16)] public readonly int DtoSize = messageSize;
  }
}