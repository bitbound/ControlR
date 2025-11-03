using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.Ipc;

public interface IIpcServer : IConnectionBase
{
  bool TryGetServerHandle([NotNullWhen(true)] out SafeHandle? handle);
  Task<bool> WaitForConnection(CancellationToken cancellationToken);
}

internal class IpcServer : ConnectionBase, IIpcServer
{
  public IpcServer(
    string pipeName,
    ICallbackStoreFactory callbackFactory,
    IContentTypeResolver contentTypeResolver,
    ILogger<IpcServer> logger)
    : base(pipeName, callbackFactory, contentTypeResolver, logger)
  {
    _pipeStream = new NamedPipeServerStream(
      pipeName,
      PipeDirection.InOut,
      NamedPipeServerStream.MaxAllowedServerInstances,
      PipeTransmissionMode.Byte,
      PipeOptions.Asynchronous);
  }

  [SupportedOSPlatform("windows")]
  public IpcServer(
    string pipeName,
    PipeSecurity pipeSecurity,
    ICallbackStoreFactory callbackFactory,
    IContentTypeResolver contentTypeResolver,
    ILogger<IpcServer> logger)
    : base(pipeName, callbackFactory, contentTypeResolver, logger)
  {
    _pipeStream = NamedPipeServerStreamAcl.Create(
      pipeName,
      PipeDirection.InOut,
      NamedPipeServerStream.MaxAllowedServerInstances,
      PipeTransmissionMode.Byte,
      PipeOptions.Asynchronous,
      0,
      0,
      pipeSecurity);
  }

  public bool TryGetServerHandle([NotNullWhen(true)] out SafeHandle? handle)
  {
    try
    {
      // PipeStream.SafePipeHandle is available cross-platform and wraps HANDLE (Windows) or fd (Unix)
      handle = _pipeStream?.SafePipeHandle;
      return handle is not null;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while retrieving server pipe handle.");
      handle = null;
      return false;
    }
  }

  public async Task<bool> WaitForConnection(CancellationToken cancellationToken)
  {
    try
    {
      await _connectLock.WaitAsync(cancellationToken);

      if (_pipeStream is null)
      {
        throw new InvalidOperationException("You must initialize the connection before calling this method.");
      }

      if (_pipeStream is NamedPipeServerStream serverStream)
      {
        await serverStream.WaitForConnectionAsync(cancellationToken);
        _logger.LogDebug("Connection established for server pipe {id}.", PipeName);
      }
      else
      {
        throw new InvalidOperationException($"{nameof(_pipeStream)} is not of type NamedPipeServerStream.");
      }

      if (!_pipeStream.IsConnected)
      {
        _logger.LogWarning("Pipe disconnected after initial acceptance.");
        return false;
      }

      return true;
    }
    catch (TaskCanceledException)
    {
      return false;
    }
    catch (OperationCanceledException)
    {
      return false;
    }
    finally
    {
      _connectLock.Release();
    }
  }
}