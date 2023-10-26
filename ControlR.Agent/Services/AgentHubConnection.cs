using Bitbound.SimpleMessenger;
using ControlR.Agent.Interfaces;
using ControlR.Agent.Messages;
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

internal interface IAgentHubConnection : IHubConnectionBase
{
    Task NotifyViewerDesktopChanged(Guid sessionId, string desktopName);

    Task SendDeviceHeartbeat();
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
     IVncSessionLauncher vncSessionLauncher,
     IAgentUpdater updater,
     IMessenger messenger,
     ILogger<AgentHubConnection> logger)
        : HubConnectionBase(scopeFactory, logger), IHostedService, IAgentHubConnection, IAgentHubClient
{
    private readonly IHostApplicationLifetime _appLifetime = appLifetime;
    private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
    private readonly ICpuUtilizationSampler _cpuSampler = cpuSampler;
    private readonly IDeviceDataGenerator _deviceCreator = deviceCreator;
    private readonly IEncryptionSessionFactory _encryptionFactory = encryptionFactory;
    private readonly IEnvironmentHelper _environmentHelper = environmentHelper;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly IMessenger _messenger = messenger;
    private readonly IProcessInvoker _processes = processes;
    private readonly IAgentUpdater _updater = updater;
    private readonly IVncSessionLauncher _vncSessionLauncher = vncSessionLauncher;

    public async Task<bool> GetVncSession(SignedPayloadDto signedDto)
    {
        try
        {
            if (!VerifyPayload(signedDto))
            {
                return false;
            }

            var dto = MessagePackSerializer.Deserialize<VncSessionRequest>(signedDto.Payload);

            if (!OperatingSystem.IsWindows() ||
                _appOptions.CurrentValue.AutoRunVnc != true)
            {
                var session = new VncSession(dto.SessionId, () => Task.CompletedTask);
                await _messenger.Send(new VncProxyRequestMessage(session));

                return true;
            }

            var result = await _vncSessionLauncher
                .CreateSession(dto.SessionId, dto.SessionPassword)
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                _logger.LogError("Failed to get streaming session.  Reason: {reason}", result.Reason);
                return false;
            }

            await _messenger.Send(new VncProxyRequestMessage(result.Value));

            return true;
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

    public async Task SendVncStream(Guid sessionId, IAsyncEnumerable<byte[]> outgoingStream)
    {
        try
        {
            await Connection.InvokeAsync("SendVncStream", sessionId, outgoingStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending VNC stream.");
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await StartImpl();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await StopConnection(cancellationToken);
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

        hubConnection.On<SignedPayloadDto, bool>(nameof(GetVncSession), GetVncSession);
    }

    private void ConfigureHttpOptions(HttpConnectionOptions options)
    {
    }

    private async Task HubConnection_Reconnected(string? arg)
    {
        await SendDeviceHeartbeat();
        await _updater.CheckForUpdate();
    }

    private async Task StartImpl()
    {
        await Connect(
            $"{AppConstants.ServerUri}/hubs/agent",
            ConfigureConnection,
            ConfigureHttpOptions,
             _appLifetime.ApplicationStopping);

        await SendDeviceHeartbeat();
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