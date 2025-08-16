using Microsoft.Extensions.Logging;
using System.IO.Pipes;

namespace ControlR.Libraries.Ipc;

public interface IIpcClient : IConnectionBase
{
  Task<bool> Connect(CancellationToken cancellationToken);
}

internal class IpcClient : ConnectionBase, IIpcClient
{
  public IpcClient(
      string serverName,
      string pipeName,
      ICallbackStoreFactory callbackFactory,
      IContentTypeResolver contentTypeResolver,
      ILogger<IpcClient> logger)
      : base(pipeName, callbackFactory, contentTypeResolver, logger)
  {
    _pipeStream = new NamedPipeClientStream(
        serverName,
        pipeName,
        PipeDirection.InOut,
        PipeOptions.Asynchronous);
  }

  public async Task<bool> Connect(CancellationToken cancellationToken)
  {
    try
    {
      await _connectLock.WaitAsync(cancellationToken);

      if (_pipeStream is NamedPipeClientStream clientPipe)
      {
        await clientPipe.ConnectAsync(cancellationToken);
        _logger.LogDebug("Connection established for client pipe {id}.", PipeName);
      }
      else
      {
        throw new InvalidOperationException("PipeStream is not of type NamedPipeClientStream.");
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to connect to IPC server.");
    }
    finally
    {
      _connectLock.Release();
    }

    return _pipeStream?.IsConnected == true;
  }
}