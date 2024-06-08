using ControlR.Libraries.Shared.Dtos.SidecarDtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Streamer.Sidecar.Services.Windows;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;
using System.Text.Json;

namespace ControlR.Streamer.Sidecar.Services;

public interface IStreamerIpcConnection
{
    Task<bool> Connect(string serverPipeName, CancellationToken cancellationToken);

    Task Send<T>(T dto) where T : SidecarDtoBase;
}
internal class StreamerIpcConnection(
    IHostApplicationLifetime _appLifetime,
    IInputSimulator _inputSimulator,
    ILogger<StreamerIpcConnection> _logger) : IStreamerIpcConnection
{
    private readonly JsonSerializerOptions _jsonOptions = new() 
    { 
        PropertyNameCaseInsensitive = true, 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
    };

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
        var json = JsonSerializer.Serialize(dto, _jsonOptions);
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
                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                _logger.LogDebug("Message received from streamer: {IpcMessage}", message);

                var baseDto = JsonSerializer.Deserialize<SidecarDtoBase>(message, _jsonOptions);
                switch (baseDto?.DtoType)
                {
                    case SidecarDtoType.MovePointer:
                        {
                            var moveDto = JsonSerializer.Deserialize<MovePointerDto>(message, _jsonOptions) ??
                                throw new JsonException("Failed to deserialize MovePointerDto.");
                            _logger.LogDebug("Received MovePointer IPC DTO: {MoveDto}", moveDto);

                            _inputSimulator.MovePointer((int)moveDto.X, (int)moveDto.Y, moveDto.MoveType);
                            break;
                        }
                    case SidecarDtoType.MouseButtonEvent:
                        {
                            var buttonDto = JsonSerializer.Deserialize<MouseButtonEventDto>(message, _jsonOptions) ??
                                                           throw new JsonException("Failed to deserialize MovePointerDto.");
                            _logger.LogDebug("Received ButtonEvent IPC DTO: {EventDto}", buttonDto);

                            _inputSimulator.InvokeMouseButtonEvent((int)buttonDto.X, (int)buttonDto.Y, buttonDto.Button, buttonDto.IsPressed);
                            break;
                        }
                    case SidecarDtoType.KeyEvent:
                        {
                            var keyDto = JsonSerializer.Deserialize<KeyEventDto>(message, _jsonOptions) ??
                                throw new JsonException("Failed to deserialize KeyEventDto.");
                            _logger.LogDebug("Received KeyEvent IPC DTO: {KeyDto}", keyDto);

                            _inputSimulator.InvokeKeyEvent(keyDto.Key, keyDto.IsPressed);
                            break;
                        }
                    case SidecarDtoType.TypeText:
                        {
                            var textDto = JsonSerializer.Deserialize<TypeTextDto>(message, _jsonOptions) ??
                                throw new JsonException("Failed to deserialize TypeTextDto.");

                            _logger.LogDebug("Received TypeText IPC DTO: {TextDto}", textDto);
                            _inputSimulator.TypeText(textDto.Text);

                            break;
                        }
                    case SidecarDtoType.ResetKeyboardState:
                        {
                            _logger.LogDebug("Received ResetKeyboardState IPC DTO.");
                            _inputSimulator.ResetKeyboardState();
                            break;
                        }
                    case SidecarDtoType.WheelScroll:
                        {
                            var wheelDto = JsonSerializer.Deserialize<WheelScrollDto>(message, _jsonOptions) ??
                                throw new JsonException("Failed to deserialize WheelScrollDto.");
                            _logger.LogDebug("Received WheelScroll IPC DTO: {WheelDto}", wheelDto);
                            _inputSimulator.ScrollWheel(
                                (int)wheelDto.X, 
                                (int)wheelDto.Y, 
                                (int)(wheelDto.ScrollY ?? 0),
                                (int)(wheelDto.ScrollX ?? 0));
                            break;
                        }
                    default:
                        _logger.LogWarning("Invalid IPC DTO type: {DtoType}", baseDto?.DtoType);
                        break;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while reading from streamer IPC pipe.");
            }
        }

        DisposeHelper.DisposeAll(Reader, Writer, Client);
        _appLifetime.StopApplication();
    }
}
