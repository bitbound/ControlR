using System.IO.Pipes;
using System.Runtime.Versioning;
using ControlR.Libraries.Ipc.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.Ipc;

public interface IIpcConnectionFactory
{
    Task<IIpcClient> CreateClient(string serverName, string pipeName);
    Task<IIpcServer> CreateServer(string pipeName);
    [SupportedOSPlatform("windows")]
    Task<IIpcServer> CreateServer(string pipeName, PipeSecurity pipeSecurity);
}

internal class IpcConnectionFactory(IServiceProvider serviceProvider) : IIpcConnectionFactory
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public Task<IIpcClient> CreateClient(string serverName, string pipeName)
    {
        var stream = new NamedPipeClientStream(
            serverName,
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        var rpcService = _serviceProvider.GetRequiredService<IDesktopClientRpcService>();
        var logger = _serviceProvider.GetRequiredService<ILogger<IpcClient>>();
        var ipcClient = new IpcClient(stream, rpcService, logger);
        return Task.FromResult<IIpcClient>(ipcClient);
    }

    public Task<IIpcServer> CreateServer(string pipeName)
    {
        var stream = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        var rpcService = _serviceProvider.GetRequiredService<IAgentRpcService>();
        var logger = _serviceProvider.GetRequiredService<ILogger<IpcServer>>();
        return Task.FromResult<IIpcServer>(new IpcServer(stream, rpcService, logger));
    }

    [SupportedOSPlatform("windows")]
    public Task<IIpcServer> CreateServer(string pipeName, PipeSecurity pipeSecurity)
    {
        var stream = NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            pipeSecurity);

        var rpcService = _serviceProvider.GetRequiredService<IAgentRpcService>();
        var logger = _serviceProvider.GetRequiredService<ILogger<IpcServer>>();
        return Task.FromResult<IIpcServer>(new IpcServer(stream, rpcService, logger));
    }
}
