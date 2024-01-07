using Bitbound.SimpleMessenger;
using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Agent.Services.Windows;
using ControlR.Devices.Common.Extensions;
using ControlR.Devices.Common.Messages;
using ControlR.Devices.Common.Native.Windows;
using ControlR.Devices.Common.Services;
using ControlR.Devices.Common.Services.Interfaces;
using ControlR.Shared.Dtos;
using ControlR.Shared.Enums;
using ControlR.Shared.Extensions;
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
     IVncSessionLauncher _vncSessionLauncher,
     IAgentUpdater _updater,
     ILocalProxyAgent _localProxy,
     IMessenger _messenger,
     ITerminalStore _terminalStore,
     IRegistryAccessor _registryAccessor,
     IOptionsMonitor<AgentAppOptions> _agentOptions,
     ILogger<AgentHubConnection> _logger)
        : HubConnectionBase(_scopeFactory, _messenger, _logger), IHostedService, IAgentHubConnection, IAgentHubClient
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

    public async Task<VncSessionRequestResult> GetVncSession(SignedPayloadDto signedDto)
    {
        try
        {
            if (!VerifySignedDto<VncSessionRequest>(signedDto, out var dto))
            {
                return new(false);
            }

            if (!_settings.AutoRunVnc)
            {
                var session = new VncSession(dto.SessionId, false);
                await _localProxy.HandleVncSession(session);

                return new(true);
            }

            var result = await _vncSessionLauncher
                .CreateSession(dto.SessionId, dto.VncPassword)
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                _logger.LogError("Failed to get streaming session.  Reason: {reason}", result.Reason);
                return new(false);
            }

            await _localProxy.HandleVncSession(result.Value);

            return new(true, result.Value.AutoRunUsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating streaming session.");
            return new(false);
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

            await Connection.InvokeAsync("UpdateDevice", result.Value);
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
            await Connection.InvokeAsync("SendTerminalOutputToViewer", viewerConnectionId, outputDto);
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

        if (_agentOptions.CurrentValue.AutoRunVnc == true)
        {
            await _vncSessionLauncher.CleanupSessions();
        }
    }

    public async Task<Result> StartRdpProxy(SignedPayloadDto requestDto)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return Result.Fail("Platform not supported.");
            }

            if (!VerifySignedDto<RdpProxyRequestDto>(requestDto, out var dto))
            {
                return Result.Fail("Signature verification failed.");
            }

            var regResult = _registryAccessor.GetRdpPort();
            if (!regResult.IsSuccess)
            {
                return regResult.ToResult();
            }

            return await _localProxy.ProxyToLocalService(dto.SessionId, regResult.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while starting RDP proxy session.");
            return Result.Fail("An error occurred while starting RDP proxy.");
        }
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

        hubConnection.On<SignedPayloadDto, VncSessionRequestResult>(nameof(GetVncSession), GetVncSession);
        hubConnection.On<SignedPayloadDto, Result<TerminalSessionRequestResult>>(nameof(CreateTerminalSession), CreateTerminalSession);
        hubConnection.On<SignedPayloadDto, Result>(nameof(ReceiveTerminalInput), ReceiveTerminalInput);
        hubConnection.On<SignedPayloadDto, Result>(nameof(StartRdpProxy), StartRdpProxy);
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
        if (_agentOptions.CurrentValue.AutoRunVnc == true)
        {
            await _vncSessionLauncher.CleanupSessions();
        }
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