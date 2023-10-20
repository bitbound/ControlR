using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Devices.Common.Native.Windows;
using ControlR.Devices.Common.Services;
using ControlR.Devices.Common.Services.Interfaces;
using ControlR.Shared;
using ControlR.Shared.Dtos;
using ControlR.Shared.Enums;
using ControlR.Shared.Extensions;
using ControlR.Shared.Interfaces.HubClients;
using ControlR.Shared.Models;
using ControlR.Shared.Services;
using MessagePack;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.Versioning;

namespace ControlR.Agent.Services;

internal interface IAgentHubConnection : IAgentHubClient, IHubConnectionBase
{
    Task NotifyViewerDesktopChanged(Guid sessionId, string desktopName);

    Task SendDeviceHeartbeat();

    Task Start();
}

internal class AgentHubConnection(
     IHostApplicationLifetime appLifetime,
     IServiceScopeFactory scopeFactory,
     IDeviceDataGenerator deviceCreator,
     IEnvironmentHelper environmentHelper,
     IProcessInvoker processes,
     IFileSystem fileSystem,
     IOptionsMonitor<AppOptions> appOptions,
     ICpuUtilizationSampler cpuSampler,
     IEncryptionSessionFactory encryptionFactory,
     IRemoteControlLauncher remoteControlLauncher,
     IAgentUpdater updater,
     ILogger<AgentHubConnection> logger) : HubConnectionBase(scopeFactory, logger), IAgentHubConnection
{
    private readonly IHostApplicationLifetime _appLifetime = appLifetime;
    private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
    private readonly ICpuUtilizationSampler _cpuSampler = cpuSampler;
    private readonly IDeviceDataGenerator _deviceCreator = deviceCreator;
    private readonly IEncryptionSessionFactory _encryptionFactory = encryptionFactory;
    private readonly IEnvironmentHelper _environmentHelper = environmentHelper;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly IProcessInvoker _processes = processes;
    private readonly IRemoteControlLauncher _remoteControlLauncher = remoteControlLauncher;
    private readonly IAgentUpdater _updater = updater;

    public async Task<bool> GetStreamingSession(SignedPayloadDto signedDto)
    {
        try
        {
            if (!VerifyPayload(signedDto))
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
                async progress =>
                {
                    if (progress == 1 || progress - downloadProgress > .05)
                    {
                        downloadProgress = progress;
                        await Connection
                            .InvokeAsync(
                                "SendRemoteControlDownloadProgress",
                                dto.StreamingSessionId,
                                dto.ViewerConnectionId,
                                downloadProgress)
                            .ConfigureAwait(false);
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

    [SupportedOSPlatform("windows")]
    public Task<WindowsSession[]> GetWindowsSessions(SignedPayloadDto signedDto)
    {
        if (!VerifyPayload(signedDto))
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
            await Connection.InvokeAsync("NotifyViewerDesktopChanged", sessionId, desktopName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending device update.");
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

            if (_appOptions.CurrentValue.AuthorizedKeys.Count == 0)
            {
                _logger.LogWarning("There are no authorized keys in appsettings. Aborting heartbeat.");
                return;
            }

            var device = await _deviceCreator.CreateDevice(
                _cpuSampler.CurrentUtilization,
                _appOptions.CurrentValue.AuthorizedKeys,
                _appOptions.CurrentValue.DeviceId);

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

    public async Task Start()
    {
        await Connect(
            $"{AppConstants.ServerUri}/hubs/agent",
            ConfigureConnection,
            ConfigureHttpOptions,
            _appLifetime.ApplicationStopping);

        await SendDeviceHeartbeat();
    }

    private void ConfigureConnection(HubConnection hubConnection)
    {
        hubConnection.Reconnected += HubConnection_Reconnected;

        if (_environmentHelper.Platform == SystemPlatform.Windows)
        {
#pragma warning disable CA1416 // Validate platform compatibility
            hubConnection.On<SignedPayloadDto, WindowsSession[]>(nameof(GetWindowsSessions), GetWindowsSessions);
#pragma warning restore CA1416 // Validate platform compatibility
        }

        hubConnection.On<SignedPayloadDto, bool>(nameof(GetStreamingSession), GetStreamingSession);
    }

    private void ConfigureHttpOptions(HttpConnectionOptions options)
    {
    }

    private async Task HubConnection_Reconnected(string? arg)
    {
        await SendDeviceHeartbeat();
        await _updater.CheckForUpdate();
    }

    private bool VerifyPayload(SignedPayloadDto signedDto)
    {
        using var session = _encryptionFactory.CreateSession();

        if (!session.Verify(signedDto))
        {
            _logger.LogCritical("Verification failed for payload with public key: {key}", signedDto.PublicKey);
            return false;
        }

        if (!_appOptions.CurrentValue.AuthorizedKeys.Contains(signedDto.PublicKeyBase64))
        {
            _logger.LogCritical("Public key does not exist in authorized keys list: {key}", signedDto.PublicKey);
            return false;
        }

        return true;
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