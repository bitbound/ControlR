using System.Net.WebSockets;
using System.Runtime.InteropServices;

namespace ControlR.Libraries.Clients.Services;

public interface IStreamingClient : IAsyncDisposable, IClosable
{
  WebSocketState State { get; }
  Task Connect(Uri websocketUri, CancellationToken cancellationToken);
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
  private readonly SemaphoreSlim _sendLock = new(1);
  protected readonly IMessenger Messenger = messenger;
  private ClientWebSocket? _client;

  protected ClientWebSocket Client => _client ?? throw new Exception("Client not initialized.");
  protected bool IsDisposed { get; private set; }

  public WebSocketState State => _client?.State ?? WebSocketState.Closed;

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
      logger.LogError(ex, "Error while closing connection.");
    }
    finally
    {
      Client.Dispose();
    }
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
        var message = new DtoReceivedMessage<DtoWrapper>(dto);
        await Messenger.Send(message);
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
    public static readonly int Size = 20;

    [FieldOffset(0)] public readonly Guid Delimiter = delimiter;

    [FieldOffset(16)] public readonly int DtoSize = messageSize;
  }
}