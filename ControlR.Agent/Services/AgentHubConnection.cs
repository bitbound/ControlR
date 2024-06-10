using Bitbound.SimpleMessenger;
using ControlR.Agent.Interfaces;
using MessagePack;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using ControlR.Devices.Native.Services;
using ControlR.Libraries.DevicesCommon.Extensions;
using ControlR.Libraries.DevicesCommon.Messages;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Hubs;
using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Interfaces.HubClients;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Agent.Services;

internal interface IAgentHubConnection : IHubConnectionBase, IHostedService
{
    Task SendDeviceHeartbeat();
    Task SendStreamerDownloadProgress(StreamerDownloadProgressDto progressDto);
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
     IStreamerLauncher _streamerLauncher,
     IStreamerUpdater _streamerUpdater,
     IAgentUpdater _agentUpdater,
     IMessenger _messenger,
     ITerminalStore _terminalStore,
     IDelayer _delayer,
     IWin32Interop _win32Interop,
     IOptionsMonitor<AgentAppOptions> _agentOptions,
     ILogger<AgentHubConnection> _logger)
        : HubConnectionBase(_scopeFactory, _messenger, _delayer, _logger), IAgentHubConnection, IAgentHubClient
{
    public async Task<bool> CreateStreamingSession(SignedPayloadDto signedDto)
    {
        try
        {
            if (!VerifySignedDto(signedDto))
            {
                return false;
            }

            var dto = MessagePackSerializer.Deserialize<StreamerSessionRequestDto>(signedDto.Payload);

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
                dto.ViewerConnectionId,
                signedDto.PublicKey,
                dto.TargetSystemSession,
                dto.NotifyUserOnSessionStart,
                dto.LowerUacDuringSession,
                dto.ViewerName)
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

        return _win32Interop.GetActiveSessions().ToArray().AsTaskResult();
    }

    public Task<Result> ReceiveAgentAppSettings(SignedPayloadDto signedDto)
    {
        try
        {
            if (!VerifySignedDto<AgentAppSettings>(signedDto, out var payload))
            {
                return Result.Fail("Signature verification failed.").AsTaskResult();
            }

            // Perform the update in a background thread after a short delay,
            // allowing the RPC call to complete okay.
            Task.Run(async () =>
            {
                await _delayer.Delay(TimeSpan.FromSeconds(1), _appLifetime.ApplicationStopping);
                await _settings.UpdateSettings(payload);
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
        _messenger.Unregister<GenericMessage<GenericMessageKind>>(this);
        _messenger.RegisterGenericMessage(this, HandleGenericMessage);
        await Connect(
              () => $"{_settings.ServerUri}hubs/agent",
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

        hubConnection.On<SignedPayloadDto, bool>(nameof(CreateStreamingSession), CreateStreamingSession);
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
        await _agentUpdater.CheckForUpdate();
        await _streamerUpdater.EnsureLatestVersion(_appLifetime.ApplicationStopping);
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