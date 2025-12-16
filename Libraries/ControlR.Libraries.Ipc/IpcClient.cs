using System.IO.Pipes;
using StreamJsonRpc;
using Microsoft.Extensions.Logging;
using ControlR.Libraries.Ipc.Interfaces;
using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Libraries.Ipc;

public interface IIpcClient : IDisposable
{
    IAgentRpcService Server { get; }

    Task Connect(CancellationToken cancellationToken);
    void Start();
    Task WaitForDisconnect(CancellationToken cancellationToken);
}

internal class IpcClient(
  NamedPipeClientStream stream, 
  IDesktopClientRpcService rpcService, 
  ILogger<IpcClient> logger) : IIpcClient
{
    private readonly ILogger<IpcClient> _logger = logger;
    private readonly IDesktopClientRpcService _rpcService = rpcService;
    private readonly NamedPipeClientStream _stream = stream;

    private JsonRpc? _jsonRpc;
    private IAgentRpcService? _server;

  public IAgentRpcService Server => _server ?? throw new InvalidOperationException("Server has not been initialized. Call Start first.");

    public async Task Connect(CancellationToken cancellationToken)
    {
        await _stream.ConnectAsync(cancellationToken);
    }

    public void Dispose()
    {
      Disposer.DisposeAll(_jsonRpc, _stream);
    }

    public void Start()
    {
      _logger.LogInformation("Starting JsonRpc IPC client.");
      var formatter = new MessagePackFormatter();
      var messageHandler = new LengthHeaderMessageHandler(_stream, _stream, formatter);
      _jsonRpc = new JsonRpc(messageHandler, _rpcService);
      _jsonRpc.StartListening();
      _server = _jsonRpc.Attach<IAgentRpcService>();
      _logger.LogInformation("JsonRpc IPC client started.");
    }

    public async Task WaitForDisconnect(CancellationToken cancellationToken)
    {
        if (_jsonRpc != null)
        {
            await _jsonRpc.Completion;
        }
    }
}
