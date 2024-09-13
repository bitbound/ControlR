using Bitbound.SimpleMessenger;
using ControlR.Agent.Interfaces;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.Versioning;
using ControlR.Devices.Native.Services;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Hubs;
using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Interfaces.HubClients;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using Microsoft.AspNetCore.Http.Connections;
using ControlR.Libraries.Clients.Services;
using ControlR.Libraries.Clients.Messages;
using ControlR.Libraries.Clients.Extensions;

namespace ControlR.Agent.Services;

internal interface IAgentHubConnection : IHubConnectionBase, IHostedService
{
    Task SendDeviceHeartbeat();
    Task SendStreamerDownloadProgress(StreamerDownloadProgressDto progressDto);
    Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto);
}

internal class AgentHubConnection(
     IHostApplicationLifetime _appLifetime,
     IServiceProvider _services,
     IDeviceDataGenerator _deviceCreator,
     IEnvironmentHelper _environmentHelper,
     ISettingsProvider _settings,
     ICpuUtilizationSampler _cpuSampler,
     IStreamerLauncher _streamerLauncher,
     IStreamerUpdater _streamerUpdater,
     IAgentUpdater _agentUpdater,
     IMessenger _messenger,
     ITerminalStore _terminalStore,
     IDelayer _delayer,
     IWin32Interop _win32Interop,
     IOptionsMonitor<AgentAppOptions> _appOptions,
     ILogger<AgentHubConnection> _logger)
        : HubConnectionBase(_services, _messenger, _delayer, _logger), IAgentHubConnection, IAgentHubClient
{
    public async Task<bool> CreateStreamingSession(StreamerSessionRequestDto dto)
    {
        try
        {
            if (!_environmentHelper.IsDebug)
            {
                var versionResult = await _streamerUpdater.EnsureLatestVersion(dto, _appLifetime.ApplicationStopping);
                if (!versionResult)
                {
                    return false;
                }
            }

            var result = await _streamerLauncher.CreateSession(
                dto.SessionId,
                dto.WebsocketUri,
                dto.ViewerConnectionId,
                dto.TargetSystemSession,
                dto.NotifyUserOnSessionStart,
                dto.ViewerName)
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                _logger.LogError("Failed to get streaming session.  Reason: {reason}", result.Reason);
            }

            _logger.LogInformation("Streaming session started.");

            return result.IsSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating streaming session.");
            return false;
        }
    }

    public async Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(TerminalSessionRequest requestDto)
    {
        try
        {
            _logger.LogInformation("Terminal session started.  Viewer Connection ID: {ConnectionId}", requestDto.ViewerConnectionId);

            return await _terminalStore.CreateSession(requestDto.TerminalId, requestDto.ViewerConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating terminal session.");
            return Result.Fail<TerminalSessionRequestResult>("An error occurred.");
        }
    }

    public Task<Result<AgentAppSettings>> GetAgentAppSettings()
    {
        try
        {
            var agentOptions = _appOptions.CurrentValue;
            var settings = new AgentAppSettings()
            {
                AppOptions = agentOptions
            };
            return Result.Ok(settings).AsTaskResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting agent appsettings.");
            return Result.Fail<AgentAppSettings>("Failed to get agent app settings.").AsTaskResult();
        }
    }
    [SupportedOSPlatform("windows6.0.6000")]
    public Task<WindowsSession[]> GetWindowsSessions()
    {
        if (_environmentHelper.Platform != SystemPlatform.Windows)
        {
            return Array.Empty<WindowsSession>().AsTaskResult();
        }

        return _win32Interop.GetActiveSessions().ToArray().AsTaskResult();
    }

    public Task<Result> ReceiveAgentAppSettings(AgentAppSettings appSettings)
    {
        try
        {
            // Perform the update in a background thread after a short delay,
            // allowing the RPC call to complete okay.
            Task.Run(async () =>
            {
                await _delayer.Delay(TimeSpan.FromSeconds(1), _appLifetime.ApplicationStopping);
                await _settings.UpdateSettings(appSettings);
                // Device heartbeat will sync authorized keys with current ones.
                await SendDeviceHeartbeat();
            }).Forget();

            return Result.Ok().AsTaskResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while saving app settings to disk.");
            return Result.Fail("Failed to save settings to disk.").AsTaskResult();
        }
    }

    public async Task<Result> ReceiveTerminalInput(TerminalInputDto dto)
    {
        try
        {
            return await _terminalStore.WriteInput(dto.TerminalId, dto.Input);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating terminal session.");
            return Result.Fail("An error occurred.");
        }
    }

    public async Task SendDeviceHeartbeat()
    {
        try
        {
            using var _ = _logger.BeginMemberScope();

            if (ConnectionState != HubConnectionState.Connected)
            {
                _logger.LogWarning("Not connected to hub when trying to send device update.");
                return;
            }

            if (_settings.AuthorizedKeys.Count == 0)
            {
                _logger.LogWarning("There are no authorized keys in appsettings. Aborting heartbeat.");
                return;
            }

            var deviceDto = await _deviceCreator.CreateDevice(
                _cpuSampler.CurrentUtilization,
                _settings.AuthorizedKeys,
                _settings.DeviceId);

            await Connection.InvokeAsync(nameof(IAgentHub.UpdateDevice), deviceDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending device update.");
        }
    }

    public async Task SendStreamerDownloadProgress(StreamerDownloadProgressDto progressDto)
    {
        await Connection.InvokeAsync(nameof(IAgentHub.SendStreamerDownloadProgress), progressDto);
    }

    public async Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto)
    {
        try
        {
            await Connection.InvokeAsync(nameof(IAgentHub.SendTerminalOutputToViewer), viewerConnectionId, outputDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending output to viewer.");
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Connect(
              () => new Uri(_settings.ServerUri, "/hubs/agent"),
              ConfigureConnection,
              ConfigureHttpOptions,
              useReconnect: true,
               _appLifetime.ApplicationStopping);

        await SendDeviceHeartbeat();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await StopConnection(cancellationToken);
    }

    private void ConfigureConnection(HubConnection hubConnection)
    {
        hubConnection.Reconnected += HubConnection_Reconnected;

        if (OperatingSystem.IsWindowsVersionAtLeast(6, 0, 6000))
        {
            hubConnection.On(nameof(GetWindowsSessions), GetWindowsSessions);
        }

        hubConnection.On<StreamerSessionRequestDto, bool>(nameof(CreateStreamingSession), CreateStreamingSession);
        hubConnection.On<TerminalSessionRequest, Result<TerminalSessionRequestResult>>(nameof(CreateTerminalSession), CreateTerminalSession);
        hubConnection.On<TerminalInputDto, Result>(nameof(ReceiveTerminalInput), ReceiveTerminalInput);
        hubConnection.On(nameof(GetAgentAppSettings), GetAgentAppSettings);
        hubConnection.On<AgentAppSettings, Result>(nameof(ReceiveAgentAppSettings), ReceiveAgentAppSettings);
    }

    private void ConfigureHttpOptions(HttpConnectionOptions options)
    {
        options.SkipNegotiation = true;
        options.Transports = HttpTransportType.WebSockets;
    }

    private async Task HubConnection_Reconnected(string? arg)
    {
        await SendDeviceHeartbeat();
        await _agentUpdater.CheckForUpdate();
        await _streamerUpdater.EnsureLatestVersion(_appLifetime.ApplicationStopping);
    }

    private class RetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            var waitSeconds = Math.Min(30, Math.Pow(retryContext.PreviousRetryCount, 2));
            return TimeSpan.FromSeconds(waitSeconds);
        }
    }
}