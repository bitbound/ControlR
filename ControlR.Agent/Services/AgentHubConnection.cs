using System.Runtime.Versioning;
using ControlR.Agent.Interfaces;
using ControlR.Devices.Native.Services;
using ControlR.Libraries.Clients.Services;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Hubs;
using ControlR.Libraries.Shared.Interfaces.HubClients;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Services;

internal interface IAgentHubConnection : IHubConnectionBase, IHostedService
{
  Task SendDeviceHeartbeat();
  Task SendStreamerDownloadProgress(StreamerDownloadProgressDto progressDto);
  Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto);
}

internal class AgentHubConnection(
  IHostApplicationLifetime appLifetime,
  IServiceProvider services,
  IDeviceDataGenerator deviceCreator,
  IEnvironmentHelper environmentHelper,
  ISettingsProvider settings,
  ICpuUtilizationSampler cpuSampler,
  IStreamerLauncher streamerLauncher,
  IStreamerUpdater streamerUpdater,
  IAgentUpdater agentUpdater,
  IMessenger messenger,
  ITerminalStore terminalStore,
  IDelayer delayer,
  IWin32Interop win32Interop,
  IOptionsMonitor<AgentAppOptions> appOptions,
  ILogger<AgentHubConnection> logger)
  : HubConnectionBase(services, messenger, delayer, logger), IAgentHubConnection, IAgentHubClient
{
  public async Task<bool> CreateStreamingSession(StreamerSessionRequestDto dto)
  {
    try
    {
      if (!environmentHelper.IsDebug)
      {
        var versionResult = await streamerUpdater.EnsureLatestVersion(dto, appLifetime.ApplicationStopping);
        if (!versionResult)
        {
          return false;
        }
      }

      var result = await streamerLauncher.CreateSession(
          dto.SessionId,
          dto.WebsocketUri,
          dto.ViewerConnectionId,
          dto.TargetSystemSession,
          dto.NotifyUserOnSessionStart,
          dto.ViewerName)
        .ConfigureAwait(false);

      if (!result.IsSuccess)
      {
        logger.LogError("Failed to get streaming session.  Reason: {reason}", result.Reason);
      }

      logger.LogInformation("Streaming session started.");

      return result.IsSuccess;
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while creating streaming session.");
      return false;
    }
  }

  public async Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(TerminalSessionRequest requestDto)
  {
    try
    {
      logger.LogInformation("Terminal session started.  Viewer Connection ID: {ConnectionId}",
        requestDto.ViewerConnectionId);

      return await terminalStore.CreateSession(requestDto.TerminalId, requestDto.ViewerConnectionId);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while creating terminal session.");
      return Result.Fail<TerminalSessionRequestResult>("An error occurred.");
    }
  }

  public Task<Result<AgentAppSettings>> GetAgentAppSettings()
  {
    try
    {
      var agentOptions = appOptions.CurrentValue;
      var settings = new AgentAppSettings
      {
        AppOptions = agentOptions
      };
      return Result.Ok(settings).AsTaskResult();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting agent appsettings.");
      return Result.Fail<AgentAppSettings>("Failed to get agent app settings.").AsTaskResult();
    }
  }

  [SupportedOSPlatform("windows6.0.6000")]
  public Task<WindowsSession[]> GetWindowsSessions()
  {
    return environmentHelper.Platform == SystemPlatform.Windows
      ? win32Interop.GetActiveSessions().ToArray().AsTaskResult()
      : Array.Empty<WindowsSession>().AsTaskResult();
  }

  public Task<Result> ReceiveAgentAppSettings(AgentAppSettings appSettings)
  {
    try
    {
      // Perform the update in a background thread after a short delay,
      // allowing the RPC call to complete okay.
      Task.Run(async () =>
      {
        await Delayer.Delay(TimeSpan.FromSeconds(1), appLifetime.ApplicationStopping);
        await settings.UpdateSettings(appSettings);
        // Device heartbeat will sync authorized keys with current ones.
        await SendDeviceHeartbeat();
      }).Forget();

      return Result.Ok().AsTaskResult();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while saving app settings to disk.");
      return Result.Fail("Failed to save settings to disk.").AsTaskResult();
    }
  }

  public async Task<Result> ReceiveTerminalInput(TerminalInputDto dto)
  {
    try
    {
      return await terminalStore.WriteInput(dto.TerminalId, dto.Input);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while creating terminal session.");
      return Result.Fail("An error occurred.");
    }
  }

  public async Task SendDeviceHeartbeat()
  {
    try
    {
      using var _ = logger.BeginMemberScope();

      if (ConnectionState != HubConnectionState.Connected)
      {
        logger.LogWarning("Not connected to hub when trying to send device update.");
        return;
      }
      
      var deviceDto = await deviceCreator.CreateDevice(
        cpuSampler.CurrentUtilization,
        settings.DeviceId);

      var updateResult = await Connection.InvokeAsync<Result<DeviceDto>>(nameof(IAgentHub.UpdateDevice), deviceDto);

      if (!updateResult.IsSuccess)
      {
        logger.LogResult(updateResult);
        return;
      }

      if (updateResult.Value.Uid != deviceDto.Uid)
      {
        logger.LogInformation("Device UID changed.  Updating appsettings.");
        await settings.UpdateUid(updateResult.Value.Uid);
      }
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while sending device update.");
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
      logger.LogError(ex, "Error while sending output to viewer.");
    }
  }

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    await Connect(
      () => new Uri(settings.ServerUri, "/hubs/agent"),
      ConfigureConnection,
      ConfigureHttpOptions,
      true,
      appLifetime.ApplicationStopping);

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
    hubConnection.On<TerminalSessionRequest, Result<TerminalSessionRequestResult>>(nameof(CreateTerminalSession),
      CreateTerminalSession);
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
    await agentUpdater.CheckForUpdate();
    await streamerUpdater.EnsureLatestVersion(appLifetime.ApplicationStopping);
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