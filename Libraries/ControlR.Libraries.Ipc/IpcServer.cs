using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;
using StreamJsonRpc;
using ControlR.Libraries.Ipc.Interfaces;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.Ipc;

public interface IIpcServer : IDisposable
{
  IDesktopClientRpcService Client { get; }
  bool IsConnected { get; }
  bool IsDisposed { get; }

  void Start();
  bool TryGetServerHandle([NotNullWhen(true)] out SafeHandle? handle);
  Task<bool> WaitForConnection(CancellationToken cancellationToken);
  Task WaitForDisconnect(CancellationToken cancellationToken);
}

internal class IpcServer(
  NamedPipeServerStream stream,
  IAgentRpcService rpcService,
  ILogger<IpcServer> logger) : IIpcServer
{
  private readonly ILogger<IpcServer> _logger = logger;
  private readonly IAgentRpcService _rpcService = rpcService;
  private readonly NamedPipeServerStream _stream = stream;

  private IDesktopClientRpcService? _client;
  private bool _isDisposed;
  private JsonRpc? _jsonRpc;

  public IDesktopClientRpcService Client => _client
      ?? throw new InvalidOperationException("Client has not been initialized.  Call Start first.");
  public bool IsConnected => _stream.IsConnected;
  public bool IsDisposed => _isDisposed;

  public void Dispose()
  {
    _isDisposed = true;
    _jsonRpc?.Dispose();
    _stream?.Dispose();
  }
  [MemberNotNull(nameof(_client))]
  public void Start()
  {
    _logger.LogInformation("Starting JsonRpc IPC server.");
    var formatter = new MessagePackFormatter();
    var messageHandler = new LengthHeaderMessageHandler(_stream, _stream, formatter);
    _jsonRpc = new JsonRpc(messageHandler, _rpcService);
    _jsonRpc.StartListening();
    _client = _jsonRpc.Attach<IDesktopClientRpcService>();
    _logger.LogInformation("JsonRpc IPC server started.");
  }
  public bool TryGetServerHandle([NotNullWhen(true)] out SafeHandle? handle)
  {
    handle = _stream.SafePipeHandle;
    return handle != null && !handle.IsInvalid;
  }
  public async Task<bool> WaitForConnection(CancellationToken cancellationToken)
  {
    await _stream.WaitForConnectionAsync(cancellationToken);
    _logger.LogInformation("Received IPC client connection.");
    return true;
  }
  public async Task WaitForDisconnect(CancellationToken cancellationToken)
  {
    if (_jsonRpc != null)
    {
      await _jsonRpc.Completion.WaitAsync(cancellationToken);
    }
  }
}
