using System.Diagnostics;
using System.Runtime.Versioning;
using ControlR.Agent.Common.Interfaces;
using ControlR.Devices.Native.Services;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Interfaces.HubClients;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services;

internal class AgentHubClient(
  IAgentHubConnection hubConnection,
  ISystemEnvironment environmentHelper,
  IStreamerLauncher streamerLauncher,
  IMessenger messenger,
  ITerminalStore terminalStore,
  IDelayer delayer,
  IWin32Interop win32Interop,
  IStreamerUpdater streamerUpdater,
  IHostApplicationLifetime appLifetime,
  IOptionsMonitor<AgentAppOptions> appOptions,
  ISettingsProvider settings,
  IProcessManager processManager,
  ILogger<AgentHubClient> logger) : IAgentHubClient
{
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly IOptionsMonitor<AgentAppOptions> _appOptions = appOptions;
  private readonly IDelayer _delayer = delayer;
  private readonly ISystemEnvironment _environmentHelper = environmentHelper;
  private readonly IAgentHubConnection _hubConnection = hubConnection;
  private readonly ILogger<AgentHubClient> _logger = logger;
  private readonly IMessenger _messenger = messenger;
  private readonly IProcessManager _processManager = processManager;
  private readonly ISettingsProvider _settings = settings;
  private readonly IStreamerLauncher _streamerLauncher = streamerLauncher;
  private readonly IStreamerUpdater _streamerUpdater = streamerUpdater;
  private readonly ITerminalStore _terminalStore = terminalStore;
  private readonly IWin32Interop _win32Interop = win32Interop;

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
      _logger.LogInformation("Terminal session started.  Viewer Connection ID: {ConnectionId}",
        requestDto.ViewerConnectionId);

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
      var settings = new AgentAppSettings
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
    return _environmentHelper.Platform == SystemPlatform.Windows
      ? _win32Interop.GetActiveSessions().ToArray().AsTaskResult()
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
        await _delayer.Delay(TimeSpan.FromSeconds(1), _appLifetime.ApplicationStopping);
        await _settings.UpdateSettings(appSettings);
        // Device heartbeat will sync authorized keys with current ones.
        await _hubConnection.SendDeviceHeartbeat();
      }).Forget();

      return Result.Ok().AsTaskResult();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while saving app settings to disk.");
      return Result.Fail("Failed to save settings to disk.").AsTaskResult();
    }
  }

  public Task ReceiveDto(DtoWrapper dto)
  {
    _messenger.Send(new DtoReceivedMessage<DtoWrapper>(dto)).Forget();
    return Task.CompletedTask;
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

  public Task UninstallAgent(string reason)
  {
    try
    {
      _logger.LogInformation("Uninstall command received.  Reason: {reason}", reason);
      var psi = new ProcessStartInfo
      {
        FileName = _environmentHelper.StartupExePath,
        Arguments = $"uninstall -i {_settings.InstanceId}",
        UseShellExecute = true
      };
      _ = _processManager.Start(psi);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while uninstalling agent.");
    }

    return Task.CompletedTask;
  }
}