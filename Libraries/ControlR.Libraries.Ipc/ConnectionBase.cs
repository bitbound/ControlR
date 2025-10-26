using System.Collections.Concurrent;
using System.IO.Pipes;
using MessagePack;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.Ipc;

public interface IConnectionBase : IDisposable
{
  bool IsConnected { get; }
  bool IsDisposed { get; }
  string PipeName { get; }

  void BeginRead(CancellationToken cancellationToken);

  Stream? GetStream();

  Task<IpcResult<TReturnType>> Invoke<TContentType, TReturnType>(TContentType content, int timeoutMs = 5000)
      where TContentType : notnull;

  void Off<TContentType>();

  void Off<TContentType>(CallbackToken callbackToken);

  CallbackToken On<TContentType>(Action<TContentType> callback);

  CallbackToken On<TContentType, ReturnType>(Func<TContentType, ReturnType> handler);

  Task Send<TContentType>(TContentType content, int timeoutMs = 5000)
       where TContentType : notnull;

  Task Send<TContentType>(TContentType content, CancellationToken cancellationToken)
       where TContentType : notnull;
  Task WaitForConnectionEnd(CancellationToken cancellationToken);
}

internal abstract class ConnectionBase(
    string pipeName,
    ICallbackStoreFactory callbackFactory,
    IContentTypeResolver contentTypeResolver,
    ILogger logger) : IConnectionBase
{
  protected readonly SemaphoreSlim _connectLock = new(1, 1);
  protected readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

  private readonly ICallbackStore _callbackStore = callbackFactory?.Create() ??
    throw new ArgumentNullException(nameof(callbackFactory));
  private readonly IContentTypeResolver _contentTypeResolver = contentTypeResolver ?? 
    throw new ArgumentNullException(nameof(contentTypeResolver));

  private readonly ConcurrentDictionary<Guid, TaskCompletionSource<MessageWrapper>> _invokesPendingCompletion = new();

  protected PipeStream? _pipeStream;

  private bool _isDisposed;
  private Task? _readTask;

  public bool IsConnected => _pipeStream?.IsConnected ?? false;
  public bool IsDisposed => _isDisposed;
  public string PipeName { get; } = pipeName;

  public void BeginRead(CancellationToken cancellationToken)
  {
    if (_isDisposed)
    {
      throw new ObjectDisposedException(nameof(ConnectionBase), "Connection has been disposed.");
    }

    if (_readTask?.IsCompleted == false)
    {
      throw new InvalidOperationException("Stream is already being read.");
    }

    _readTask = ReadFromStream(cancellationToken);
  }

  public void Dispose()
  {
    if (_isDisposed)
    {
      return;
    }
    _isDisposed = true;
    try
    {
      _pipeStream?.Close();
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Error while closing pipe stream.");
    }
    _pipeStream?.Dispose();
  }

  public Stream? GetStream()
  {
    return _pipeStream;
  }

  public async Task<IpcResult<TReturnType>> Invoke<TReturnType>(MessageWrapper wrapper, int timeoutMs = 5000)
  {
    try
    {
      var tcs = new TaskCompletionSource<MessageWrapper>();
      if (!_invokesPendingCompletion.TryAdd(wrapper.Id, tcs))
      {
        _logger.LogWarning("Already waiting for invoke completion of message ID {id}.", wrapper.Id);
        return IpcResult.Fail<TReturnType>($"Already waiting for invoke completion of message ID {wrapper.Id}.");
      }

      await SendInternal(wrapper, timeoutMs);

      await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));

      if (!tcs.Task.IsCompleted)
      {
        _logger.LogWarning("Timed out while invoking message type {contentType}.", wrapper.ContentTypeName);

        return IpcResult.Fail<TReturnType>("Timed out while invoking message.");
      }

      var result = tcs.Task.Result;

      var resultContentType = _contentTypeResolver.ResolveType(result.ContentTypeName);
      if (resultContentType is null)
      {
        return IpcResult.Fail<TReturnType>("Content type is null in response.");
      }

      var deserialized = MessagePackSerializer.Deserialize(resultContentType, result.Content);
      if (deserialized is TReturnType typedResult)
      {
        return IpcResult.Ok(typedResult);
      }
      return IpcResult.Fail<TReturnType>("Failed to deserialize message.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while invoking.");
      return IpcResult.Fail<TReturnType>(ex);
    }
    finally
    {
      _invokesPendingCompletion.TryRemove(wrapper.Id, out _);
    }
  }

  public Task<IpcResult<TReturnType>> Invoke<TContentType, TReturnType>(TContentType content, int timeoutMs = 5000)
      where TContentType : notnull
  {
    var wrapper = new MessageWrapper(typeof(TContentType), content, MessageType.Invoke);

    return Invoke<TReturnType>(wrapper, timeoutMs);
  }

  public void Off<TContentType>()
  {
    if (!_callbackStore.TryRemoveAll(typeof(TContentType)))
    {
      _logger.LogWarning("The message type {contentType} wasn't found in the callback colection.", typeof(TContentType));
    }
  }

  public void Off<TContentType>(CallbackToken callbackToken)
  {
    if (!_callbackStore.TryRemove(typeof(TContentType), callbackToken))
    {
      _logger.LogWarning("The message type {contentType} wasn't found in the callback colection.", typeof(TContentType));
    }
  }

  public CallbackToken On<TContentType>(Action<TContentType> callback)
  {
    ArgumentNullException.ThrowIfNull(callback);

    var objectCallback = new Action<object>(x => callback((TContentType)x));

    return _callbackStore.Add(typeof(TContentType), objectCallback);
  }

  public CallbackToken On<TContentType, ReturnType>(Func<TContentType, ReturnType> handler)
  {
    ArgumentNullException.ThrowIfNull(handler);

    var objectHandler = new Func<object, object>(x =>
        handler((TContentType)x) ?? throw new InvalidOperationException("Handler returned null."));

    return _callbackStore.Add(objectHandler, typeof(TContentType), typeof(ReturnType));
  }

  public Task Send<TContentType>(TContentType content, int timeoutMs = 5000)
      where TContentType : notnull
  {
    var wrapper = new MessageWrapper(typeof(TContentType), content, MessageType.Send);
    return SendInternal(wrapper, timeoutMs);
  }

  public Task Send<TContentType>(TContentType content, CancellationToken cancellationToken)
    where TContentType : notnull
  {
    var wrapper = new MessageWrapper(typeof(TContentType), content, MessageType.Send);
    return SendInternal(wrapper, cancellationToken);
  }

  public async Task WaitForConnectionEnd(CancellationToken cancellationToken)
  {
    if (_readTask is null)
    {
      throw new InvalidOperationException("Connection has not been started. Call BeginRead first.");
    }

    if (_readTask.IsCompleted)
    {
      throw new InvalidOperationException("Connection has already ended.");
    }

    await _readTask.WaitAsync(cancellationToken);
  }

  private async Task ProcessMessage(MessageWrapper wrapper)
  {
    switch (wrapper.MessageType)
    {
      case MessageType.Response:
        {
          if (_invokesPendingCompletion.TryGetValue(wrapper.ResponseTo, out var tcs))
          {
            tcs.SetResult(wrapper);
          }
          break;
        }
      case MessageType.Send:
        {
          await _callbackStore.InvokeActions(wrapper);
          break;
        }
      case MessageType.Invoke:
        {
          await _callbackStore.InvokeFuncs(wrapper, async result =>
          {
            await SendInternal(result);
          });
          break;
        }
      case MessageType.Unspecified:
      default:
        _logger.LogWarning("Unexpected message type: {messageType}", wrapper.MessageType);
        break;
    }
  }

  private async Task ReadFromStream(CancellationToken cancellationToken)
  {
    while (_pipeStream?.IsConnected == true)
    {
      try
      {
        if (cancellationToken.IsCancellationRequested)
        {
          _logger.LogDebug("IPC connection read cancellation requested.  Pipe Name: {pipeName}", PipeName);
          break;
        }

        // Check if the pipe is still connected before attempting to read
        if (!_pipeStream.IsConnected)
        {
          _logger.LogInformation("Pipe stream is no longer connected.");
          break;
        }

        var messageSizeBuffer = new byte[4];
        var sizeBytesRead = 0;

        // Read the 4-byte message size header
        while (sizeBytesRead < 4)
        {
          var bytesRead = await _pipeStream.ReadAsync(messageSizeBuffer.AsMemory(sizeBytesRead, 4 - sizeBytesRead), cancellationToken);
          if (bytesRead == 0)
          {
            // Stream was closed by the other end
            _logger.LogInformation("Pipe stream was closed while reading message size header.");
            break;
          }
          sizeBytesRead += bytesRead;
        }

        var messageSize = BitConverter.ToInt32(messageSizeBuffer, 0);

        if (messageSize <= 0 || messageSize > 100_000_000) // 100MB max message size
        {
          _logger.LogWarning("Invalid message size received: {messageSize}. Closing connection.", messageSize);
          break;
        }

        var buffer = new byte[messageSize];
        var messageBytesRead = 0;

        // Read the message content
        while (messageBytesRead < messageSize)
        {
          var bytesRead = await _pipeStream.ReadAsync(buffer.AsMemory(messageBytesRead, messageSize - messageBytesRead), cancellationToken);
          if (bytesRead == 0)
          {
            // Stream was closed by the other end
            _logger.LogInformation("Pipe stream was closed while reading message content.");
            break;
          }
          messageBytesRead += bytesRead;
        }

        var wrapper = MessagePackSerializer.Deserialize<MessageWrapper>(buffer, cancellationToken: cancellationToken);

        await ProcessMessage(wrapper);
      }
      catch (ThreadAbortException ex)
      {
        _logger.LogInformation(ex, "IPC connection aborted.  Pipe Name: {pipeName}", PipeName);
        break;
      }
      catch (TaskCanceledException)
      {
        _logger.LogInformation("Pipe read operation was cancelled.");
        break;
      }
      catch (MessagePackSerializationException ex) when (ex.InnerException is EndOfStreamException)
      {
        _logger.LogInformation("Pipe was closed at the other end.");
        break;
      }
      catch (IOException ex)
      {
        _logger.LogInformation(ex, "IO error occurred, likely due to pipe being closed by the other end.");
        break;
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("pipe") || ex.Message.Contains("stream"))
      {
        _logger.LogInformation(ex, "Pipe operation failed, likely due to connection being closed.");
        break;
      }
      catch (Exception ex) when (ex.Message == "The operation was canceled.")
      {
        _logger.LogInformation("Pipe read operation was cancelled.");
        break;
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "Failed to process pipe message.");
        break;
      }
    }

    _logger.LogInformation("IPC stream reading ended. Pipe Name: {pipeName}", PipeName);
  }

  private async Task SendInternal(MessageWrapper wrapper, int timeoutMs = 5000)
  {
    if (timeoutMs < 1)
    {
      throw new ArgumentException("Timeout must be greater than 0.");
    }

    using var cts = new CancellationTokenSource(timeoutMs);
    await SendInternal(wrapper, cts.Token);
  }

  private async Task SendInternal(MessageWrapper wrapper, CancellationToken cancellationToken)
  {
    try
    {
      if (_pipeStream is null)
      {
        throw new InvalidOperationException("Pipe stream hasn't been created yet.");
      }

      var wrapperBytes = MessagePackSerializer.Serialize(wrapper, cancellationToken: cancellationToken);

      var messageSizeBuffer = BitConverter.GetBytes(wrapperBytes.Length);
      await _pipeStream.WriteAsync(messageSizeBuffer, cancellationToken);
      await _pipeStream.WriteAsync(wrapperBytes, cancellationToken);
      await _pipeStream.FlushAsync(cancellationToken);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Error sending message.  Content Type: {contentType}", wrapper.ContentTypeName);
    }
  }
}