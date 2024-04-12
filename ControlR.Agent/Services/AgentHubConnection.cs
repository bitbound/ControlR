using Bitbound.SimpleMessenger;
using ControlR.Agent.Interfaces;
using ControlR.Agent.Native.Windows;
using ControlR.Devices.Common.Extensions;
using ControlR.Devices.Common.Messages;
using ControlR.Devices.Common.Services;
using ControlR.Shared.Dtos;
using ControlR.Shared.Enums;
using ControlR.Shared.Extensions;
using ControlR.Shared.Hubs;
using ControlR.Shared.Interfaces.HubClients;
using ControlR.Shared.Models;
using ControlR.Shared.Primitives;
using ControlR.Shared.Services;
using ControlR.Viewer.Models.Messages;
using MessagePack;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;

namespace ControlR.Agent.Services;

internal interface IAgentHubConnection : IHubConnectionBase
{
    Task NotifyViewerDesktopChanged(Guid sessionId, string desktopName);
    Task SendDeviceHeartbeat();

    Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto);
}

internal class AgentHubConnection(
     IHostApplicationLifetime _appLifetime,
     IServiceScopeFactory _scopeFactory,
     IDeviceDataGenerator _deviceCreator,
     IEnvironmentHelper _environmentHelper,
     ISettingsProvider _settings,
     ICpuUtilizationSampler _cpuSampler,
     IKeyProvider _keyProvider,
     IRemoteControlLauncher _remoteControlLauncher,
     IAgentUpdater _updater,
     IMessenger _messenger,
     ITerminalStore _terminalStore,
     IDelayer _delayer,
     IOptionsMonitor<AgentAppOptions> _agentOptions,
     ILogger<AgentHubConnection> _logger)
        : HubConnectionBase(_scopeFactory, _messenger, _delayer, _logger), IHostedService, IAgentHubConnection, IAgentHubClient
{
    public async Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(SignedPayloadDto requestDto)
    {
        try
        {
            if (!VerifySignedDto<TerminalSessionRequest>(requestDto, out var payload))
            {
                return Result.Fail<TerminalSessionRequestResult>("Signature verification failed.");
            }

            return await _terminalStore.CreateSession(payload.TerminalId, payload.ViewerConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating terminal session.");
            return Result.Fail<TerminalSessionRequestResult>("An error occurred.");
        }
    }

    public Task<Result<AgentAppSettings>> GetAgentAppSettings(SignedPayloadDto signedDto)
    {
        try
        {
            if (!VerifySignedDto(signedDto))
            {
                return Result.Fail<AgentAppSettings>("Signature verification failed.").AsTaskResult();
            }

            var agentOptions = _agentOptions.CurrentValue;
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

    public async Task<bool> GetStreamingSession(SignedPayloadDto signedDto)
    {
        try
        {
            if (!VerifySignedDto(signedDto))
            {
                return false;
            }

            var dto = MessagePackSerializer.Deserialize<StreamerSessionRequestDto>(signedDto.Payload);
            
            double downloadProgress = 0;

            var result = await _remoteControlLauncher.CreateSession(
                dto.StreamingSessionId,
                signedDto.PublicKey,
                dto.TargetSystemSession,
                dto.TargetDesktop ?? string.Empty,
                dto.NotifyUserOnSessionStart,
                dto.ViewerName,
                async progress =>
                {
                    try
                    {
                        if (progress == 1 || progress - downloadProgress > .05)
                        {
                            downloadProgress = progress;
                            await Connection
                                .InvokeAsync(
                                    nameof(IAgentHub.SendRemoteControlDownloadProgress),
                                    dto.StreamingSessionId,
                                    dto.ViewerConnectionId,
                                    downloadProgress)
                                .ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error while downloading remote control binaries.");
                    }
                })
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                _logger.LogError("Failed to get streaming session.  Reason: {reason}", result.Reason);
            }

            return result.IsSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating streaming session.");
            return false;
        }
    }

    [SupportedOSPlatform("windows6.0.6000")]
    public Task<WindowsSession[]> GetWindowsSessions(SignedPayloadDto signedDto)
    {
        if (!VerifySignedDto(signedDto))
        {
            return Array.Empty<WindowsSession>().AsTaskResult();
        }

        if (_environmentHelper.Platform != SystemPlatform.Windows)
        {
            return Array.Empty<WindowsSession>().AsTaskResult();
        }

        return Win32.GetActiveSessions().ToArray().AsTaskResult();
    }

    public async Task NotifyViewerDesktopChanged(Guid sessionId, string desktopName)
    {
        try
        {
            await Connection.InvokeAsync(nameof(IAgentHub.NotifyViewerDesktopChanged), sessionId, desktopName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending device update.");
        }
    }
    public async Task<Result> ReceiveAgentAppSettings(SignedPayloadDto signedDto)
    {
        try
        {
            if (!VerifySignedDto<AgentAppSettings>(signedDto, out var payload))
            {
                return Result.Fail("Signature verification failed.");
            }

            await _settings.UpdateSettings(payload);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while saving app settings to disk.");
            return Result.Fail("Failed to save settings to disk.");
        }
    }

    public async Task<Result> ReceiveTerminalInput(SignedPayloadDto dto)
    {
        try
        {
            if (!VerifySignedDto<TerminalInputDto>(dto, out var payload))
            {
                return Result.Fail("Signature verification failed.");
            }

            return await _terminalStore.WriteInput(payload.TerminalId, payload.Input);
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

            var device = await _deviceCreator.CreateDevice(
                _cpuSampler.CurrentUtilization,
                _settings.AuthorizedKeys,
                _settings.DeviceId);

            var result = device.TryCloneAs<Device, DeviceDto>();

            if (!result.IsSuccess)
            {
                _logger.LogResult(result);
                return;
            }

            await Connection.InvokeAsync(nameof(IAgentHub.UpdateDevice), result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending device update.");
        }
    }

    public async Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto)
    {
        try
        {
            await Connection.InvokeAsync(nameof(IAgentHub.SendTerminalOutputToViewer), viewerConnectionId, outputDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending VNC stream.");
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _messenger.Unregister<GenericMessage<GenericMessageKind>>(this);
        _messenger.RegisterGenericMessage(this, HandleGenericMessage);
        await Connect(
              $"{_settings.ServerUri}hubs/agent",
              ConfigureConnection,
              ConfigureHttpOptions,
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
            hubConnection.On<SignedPayloadDto, WindowsSession[]>(nameof(GetWindowsSessions), GetWindowsSessions);
        }

        hubConnection.On<SignedPayloadDto, bool>(nameof(GetStreamingSession), GetStreamingSession);
        hubConnection.On<SignedPayloadDto, Result<TerminalSessionRequestResult>>(nameof(CreateTerminalSession), CreateTerminalSession);
        hubConnection.On<SignedPayloadDto, Result>(nameof(ReceiveTerminalInput), ReceiveTerminalInput);
        hubConnection.On<SignedPayloadDto, Result<AgentAppSettings>>(nameof(GetAgentAppSettings), GetAgentAppSettings);
        hubConnection.On<SignedPayloadDto, Result>(nameof(ReceiveAgentAppSettings), ReceiveAgentAppSettings);
    }

    private void ConfigureHttpOptions(HttpConnectionOptions options)
    {
    }

    private async Task HandleGenericMessage(object subscriber, GenericMessageKind kind)
    {
        if (kind == GenericMessageKind.ServerUriChanged)
        {
            await StopAsync(_appLifetime.ApplicationStopping);
            await StartAsync(_appLifetime.ApplicationStopping);
        }
    }

    private async Task HubConnection_Reconnected(string? arg)
    {
        await SendDeviceHeartbeat();
        await _updater.CheckForUpdate();
    }

    private bool VerifySignedDto(SignedPayloadDto signedDto)
    {
        if (!_keyProvider.Verify(signedDto))
        {
            _logger.LogCritical("Verification failed for payload with public key: {key}", signedDto.PublicKey);
            return false;
        }

        if (!_settings.AuthorizedKeys.Contains(signedDto.PublicKeyBase64))
        {
            _logger.LogCritical("Public key does not exist in authorized keys list: {key}", signedDto.PublicKey);
            return false;
        }
        return true;
    }

    private bool VerifySignedDto<TPayload>(
      SignedPayloadDto signedDto,
      [NotNullWhen(true)] out TPayload? payload)
    {
        payload = default;

        if (!VerifySignedDto(signedDto))
        {
            return false;
        }

        payload = MessagePackSerializer.Deserialize<TPayload>(signedDto.Payload);
        return payload is not null;
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