using Bitbound.SimpleMessenger;
using ControlR.Shared.Extensions;
using ControlR.Streamer.Sidecar.IpcDtos;
using ControlR.Streamer.Sidecar.Options;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ControlR.Streamer.Sidecar.Services;

public interface IStreamerIpcConnection
{
    Task Send<T>(T dto)
        where T : SidecarDtoBase;
    Task<bool> Connect(string serverPipeName, CancellationToken cancellationToken);
}
internal class StreamerIpcConnection(
    IHostApplicationLifetime _appLifetime,
    ILogger<StreamerIpcConnection> _logger) : IStreamerIpcConnection
{
    private NamedPipeClientStream? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    private NamedPipeClientStream Client => _client ?? throw new InvalidOperationException("IPC client has not been initialized.");
    private StreamReader Reader => _reader ?? throw new InvalidOperationException("IPC stream reader has not been initialized.");
    private StreamWriter Writer => _writer ?? throw new InvalidOperationException("IPC stream writer has not been initialized.");

    public async Task<bool> Connect(string serverPipeName, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Connecting to streamer pipe server. Pipe Name: {PipeName}", serverPipeName);

        _client = new NamedPipeClientStream(".", serverPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, connectCts.Token);

        try
        {
            await _client.ConnectAsync(linkedCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to streamer pipe server.");
            return false;
        }

        _reader = new StreamReader(_client, leaveOpen: true);
        _writer = new StreamWriter(_client, leaveOpen: true);

        _logger.LogInformation("Connected to streamer pipe server.");

        ReadFromStream().Forget();

        return _client.IsConnected;
    }

    public async Task Send<T>(T dto) where T : SidecarDtoBase
    {
        var json = JsonSerializer.Serialize(dto);
        await Writer.WriteAsync(json);
        await Writer.FlushAsync();
    }

    private async Task ReadFromStream()
    {
        while (Client.IsConnected)
        {
            try
            {
                var message = await Reader.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    var baseDto = JsonSerializer.Deserialize<SidecarDtoBase>(message);
                    switch (baseDto?.DtoType)
                    {
                        default:
                            _logger.LogWarning("Invalid IPC DTO type: {DtoType}", baseDto?.DtoType);
                            break;
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while reading from streamer IPC pipe.");
            }
        }

        Reader.Dispose();
        Writer.Dispose();
        Client.Dispose();
        _appLifetime.StopApplication();
    }
}
