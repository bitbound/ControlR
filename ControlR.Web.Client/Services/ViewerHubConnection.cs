﻿using System.Runtime.CompilerServices;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace ControlR.Web.Client.Services;

public interface IViewerHubConnection
{
  HubConnectionState ConnectionState { get; }
  bool IsConnected { get; }
  Task CloseTerminalSession(Guid deviceId, Guid terminalId);

  Task Connect(CancellationToken cancellationToken = default);

  Task<Result> CreateTerminalSession(Guid deviceId, Guid terminalId);
  Task<Result<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto requestDto);

  Task<Result<ServerStatsDto>> GetServerStats();
  Task<Uri?> GetWebSocketRelayOrigin();
  Task<Result<WindowsSession[]>> GetWindowsSessions(Guid deviceId);

  Task InvokeCtrlAltDel(Guid deviceId);
  Task RefreshDeviceInfo(Guid deviceId);
  Task<Result> RequestStreamingSession(
    Guid deviceId,
    Guid sessionId,
    Uri websocketUri,
    int targetSystemSession);

  Task<Result> RequestVncSession(
   Guid deviceId,
   Guid sessionId,
   Uri websocketUri);

  Task SendAgentUpdateTrigger(Guid deviceId);
  Task SendPowerStateChange(Guid deviceId, PowerStateChangeType powerStateType);
  Task<Result> SendTerminalInput(Guid deviceId, Guid terminalId, string input);
  Task SendWakeDevice(string[] macAddresses);
  Task UninstallAgent(Guid deviceId, string reason);
}

internal class ViewerHubConnection(
  NavigationManager navMan,
  IHubConnection<IViewerHub> viewerHub,
  IBusyCounter busyCounter,
  ISettings settings,
  IMessenger messenger,
  IDelayer delayer,
  ILogger<ViewerHubConnection> logger) : IViewerHubConnection
{
  private readonly IBusyCounter _busyCounter = busyCounter;
  private readonly IDelayer _delayer = delayer;
  private readonly ILogger<ViewerHubConnection> _logger = logger;
  private readonly IMessenger _messenger = messenger;
  private readonly NavigationManager _navMan = navMan;
  private readonly ISettings _settings = settings;
  private readonly IHubConnection<IViewerHub> _viewerHub = viewerHub;

  public HubConnectionState ConnectionState => _viewerHub.ConnectionState;
  public bool IsConnected => _viewerHub.IsConnected;

  public async Task CloseTerminalSession(Guid deviceId, Guid terminalId)
  {
    await TryInvoke(async () =>
    {
      var request = new CloseTerminalRequestDto(terminalId);
      var wrapper = DtoWrapper.Create(request, DtoType.CloseTerminalRequest);
      await _viewerHub.Server.SendDtoToAgent(deviceId, wrapper);
    });
  }

  public async Task Connect(CancellationToken cancellationToken = default)
  {
    using var _ = _busyCounter.IncrementBusyCounter();

    while (true)
    {
      var result = await _viewerHub.Connect(
        new Uri($"{_navMan.BaseUri}hubs/viewer"),
        true,
        cancellationToken: cancellationToken);

      if (result)
      {
        break;
      }
    }

    _viewerHub.Closed += Connection_Closed;
    _viewerHub.Reconnecting += Connection_Reconnecting;
    _viewerHub.Reconnected += Connection_Reconnected;
    _viewerHub.ConnectThrew += Connection_Threw;
    await _messenger.Send(new HubConnectionStateChangedMessage(_viewerHub.ConnectionState));
  }

  public async Task<Result> CreateTerminalSession(
    Guid deviceId,
    Guid terminalId)
  {
    return await TryInvoke(
      async () =>
      {
        Guard.IsNotNullOrWhiteSpace(_viewerHub.ConnectionId);

        var request = new TerminalSessionRequest(terminalId, _viewerHub.ConnectionId);

        return await _viewerHub.Server.CreateTerminalSession(deviceId, request);
      },
      () => Result.Fail("Failed to create terminal session."));
  }

  public async Task<Result<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto requestDto)
  {
    return await TryInvoke(
      async () =>
      {
        Guard.IsNotNullOrWhiteSpace(_viewerHub.ConnectionId);

        var result = await _viewerHub.Server.GetPwshCompletions(requestDto);
        if (!result.IsSuccess)
        {
          _logger.LogResult(result);
        }

        return result;
      },
      () => Result.Fail<PwshCompletionsResponseDto>("Failed to get PowerShell command completions."));
  }

  public async Task<Result<ServerStatsDto>> GetServerStats()
  {
    return await TryInvoke(
      async () =>
      {
        await WaitForConnection();
        var result = await _viewerHub.Server.GetServerStats();
        if (!result.IsSuccess)
        {
          _logger.LogResult(result);
        }

        return result;
      },
      () => Result.Fail<ServerStatsDto>("Failed to get server stats."));
  }

  public async Task<Uri?> GetWebSocketRelayOrigin()
  {
    return await TryInvoke(
      _viewerHub.Server.GetWebSocketRelayOrigin,
      () => null);
  }

  public async Task<Result<WindowsSession[]>> GetWindowsSessions(Guid deviceId)
  {
    try
    {
      var sessions = await _viewerHub.Server.GetWindowsSessions(deviceId);
      return Result.Ok(sessions);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting windows sessions.");
      return Result.Fail<WindowsSession[]>(ex);
    }
  }

  public async Task InvokeCtrlAltDel(Guid deviceId)
  {
    await TryInvoke(async () =>
    {
      var dto = new InvokeCtrlAltDelRequestDto();
      var dtoWrapper = DtoWrapper.Create(dto, DtoType.InvokeCtrlAltDel);
      await _viewerHub.Server.SendDtoToAgent(deviceId, dtoWrapper);
    });
  }

  public async Task RefreshDeviceInfo(Guid deviceId)
  {
    await TryInvoke(async () =>
    {
      var dto = new RefreshDeviceInfoRequestDto();
      var wrapper = DtoWrapper.Create(dto, DtoType.RefreshDeviceInfoRequest);
      await _viewerHub.Server.SendDtoToAgent(deviceId, wrapper);
    });
  }

  public async Task<Result> RequestStreamingSession(
    Guid deviceId,
    Guid sessionId,
    Uri websocketUri,
    int targetSystemSession)
  {
    try
    {
      if (_viewerHub.ConnectionId is null)
      {
        return Result.Fail("Connection has closed.");
      }

      var notifyUser = await _settings.GetNotifyUserOnSessionStart();
      var requestDto = new StreamerSessionRequestDto(
        sessionId,
        websocketUri,
        targetSystemSession,
        _viewerHub.ConnectionId,
        deviceId,
        notifyUser);

      var result = await _viewerHub.Server.RequestStreamingSession(deviceId, requestDto);

      return result.Log(_logger);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting remote streaming session.");
      return Result.Fail(ex);
    }
  }

  public async Task<Result> RequestVncSession(Guid deviceId, Guid sessionId, Uri websocketUri)
  {
    try
    {
      if (_viewerHub.ConnectionId is null)
      {
        return Result.Fail("Connection has closed.");
      }

      var notifyUser = await _settings.GetNotifyUserOnSessionStart();
      var requestDto = new VncSessionRequestDto(
        sessionId,
        websocketUri,
        _viewerHub.ConnectionId,
        deviceId,
        notifyUser);

      var result = await _viewerHub.Server.RequestVncSession(deviceId, requestDto);

      return result.Log(_logger);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting remote streaming session.");
      return Result.Fail(ex);
    }
  }

  public async Task SendAgentUpdateTrigger(Guid deviceId)
  {
    await TryInvoke(async () =>
    {
      var dto = new TriggerAgentUpdateDto();
      var wrapper = DtoWrapper.Create(dto, DtoType.TriggerAgentUpdate);
      await _viewerHub.Server.SendDtoToAgent(deviceId, wrapper);
    });
  }

  public async Task SendPowerStateChange(Guid deviceId, PowerStateChangeType powerStateType)
  {
    await TryInvoke(async () =>
    {
      var powerDto = new PowerStateChangeDto(powerStateType);
      var wrapper = DtoWrapper.Create(powerDto, DtoType.PowerStateChange);
      await _viewerHub.Server.SendDtoToAgent(deviceId, wrapper);
    });
  }

  public async Task<Result> SendTerminalInput(Guid deviceId, Guid terminalId, string input)
  {
    return await TryInvoke(
      async () =>
      {
        var request = new TerminalInputDto(terminalId, input);
        return await _viewerHub.Server.SendTerminalInput(deviceId, request);
      },
      () => Result.Fail("Failed to send terminal input"));
  }

  public async Task SendWakeDevice(string[] macAddresses)
  {
    await TryInvoke(
      async () =>
      {
        var request = new WakeDeviceDto(macAddresses);
        var wrapper = DtoWrapper.Create(request, DtoType.WakeDevice);
        await _viewerHub.Server.SendDtoToUserGroups(wrapper);
      });
  }

  public async Task UninstallAgent(Guid deviceId, string reason)
  {
    await TryInvoke(
      async () => { await _viewerHub.Server.UninstallAgent(deviceId, reason); });
  }

  private async Task Connection_Closed(Exception? arg)
  {
    await _messenger.Send(new HubConnectionStateChangedMessage(_viewerHub.ConnectionState));
  }

  private async Task Connection_Reconnected(string? arg)
  {
    await _messenger.Send(new HubConnectionStateChangedMessage(_viewerHub.ConnectionState));
  }

  private async Task Connection_Reconnecting(Exception? arg)
  {
    await _messenger.Send(new HubConnectionStateChangedMessage(_viewerHub.ConnectionState));
  }

  private async Task Connection_Threw(Exception ex)
  {
    await _messenger.Send(new ToastMessage(ex.Message, Severity.Error));
  }

  private async Task TryInvoke(Func<Task> func, [CallerMemberName] string callerName = "")
  {
    try
    {
      using var _ = _logger.BeginScope(callerName);
      await func.Invoke();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while invoking hub method.");
    }
  }

  private async Task<T> TryInvoke<T>(Func<Task<T>> func, Func<T> defaultValue,
    [CallerMemberName] string callerName = "")
  {
    try
    {
      using var _ = _logger.BeginScope(callerName);
      return await func.Invoke();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while invoking hub method.");
      return defaultValue();
    }
  }

  private async Task WaitForConnection()
  {
    await _delayer.WaitForAsync(() => _viewerHub.IsConnected);
  }
}