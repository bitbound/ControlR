using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Models;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Services;

public interface IRemoteControlHostManager
{
  Task<Result> StartHost(RemoteControlRequestIpcDto requestDto);
  Task StopAllHosts(string reason);
  Task StopHost(Guid sessionId);
  bool TryGetSession(Guid sessionId, [NotNullWhen(true)] out RemoteControlSession? session);
}

public class RemoteControlHostManager(
  IUserInteractionService userInteractionService,
  IOptionsMonitor<DesktopClientOptions> desktopClientOptions,
  IIpcClientAccessor ipcClientAccessor,
  ILogger<RemoteControlHostManager> logger) : IRemoteControlHostManager
{
  private readonly IOptionsMonitor<DesktopClientOptions> _desktopClientOptions = desktopClientOptions;
  private readonly IIpcClientAccessor _ipcClientAccessor = ipcClientAccessor;
  private readonly ILogger<RemoteControlHostManager> _logger = logger;
  private readonly ConcurrentDictionary<Guid, RemoteControlSession> _sessions = new();
  private readonly IUserInteractionService _userInteractionService = userInteractionService;

  public async Task<Result> StartHost(RemoteControlRequestIpcDto requestDto)
  {
    try
    {
      _logger.LogInformation(
        "Handling remote control request. Session ID: {SessionId}, Viewer Connection ID: {ViewerConnectionId}, " +
        "Target System Session: {TargetSystemSession}, Process ID: {TargetProcessId}, Viewer Name: {ViewerName}",
        requestDto.SessionId,
        requestDto.ViewerConnectionId,
        requestDto.TargetSystemSession,
        requestDto.TargetProcessId,
        requestDto.ViewerName);

      var builder = CreateRemoteControlHostBuilder(requestDto);
      var app = builder.Build();
      var session = CreateRemoteControlSession(requestDto, app);
      RegisterHostLifetimeHandlers(app, requestDto, session);
      await app.StartAsync(session.CancellationTokenSource.Token);
      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling remote control request.");
      return Result.Fail(ex, "An error occurred while starting the remote control session.");
    }
  }

  public async Task StopAllHosts(string reason)
  {
    foreach (var session in _sessions.Values)
    {
      try
      {
        await session.CancellationTokenSource.CancelAsync();
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "Failed to stop remote control session.");
      }
    }
    _sessions.Clear();
  }

  public async Task StopHost(Guid sessionId)
  {
    if (!_sessions.TryGetValue(sessionId, out var session))
    {
      _logger.LogWarning("No remote control session found for Session ID: {SessionId}", sessionId);
      return;
    }
    _logger.LogInformation("Stopping remote control session for Session ID: {SessionId}", sessionId);
    await session.CancellationTokenSource.CancelAsync();
  }

  public bool TryGetSession(Guid sessionId, [NotNullWhen(true)] out RemoteControlSession? session)
  {
    return _sessions.TryGetValue(sessionId, out session);
  }

  internal HostApplicationBuilder CreateRemoteControlHostBuilder(RemoteControlRequestIpcDto requestDto)
  {
    var builder = Host.CreateApplicationBuilder();
    builder.AddCommonDesktopServices<Toaster>(
      _ipcClientAccessor,
      _userInteractionService,
      appBuilder =>
      {
        appBuilder.Services
          .AddSingleton<IToaster, Toaster>()
          .AddSingleton(_userInteractionService)
          .AddSingleton(_ipcClientAccessor);
      },
      options =>
      {
        options.SessionId = requestDto.SessionId;
        options.NotifyUser = requestDto.NotifyUserOnSessionStart;
        options.RequireConsent = requestDto.RequireConsent;
        options.ViewerName = requestDto.ViewerName;
        options.ViewerConnectionId = requestDto.ViewerConnectionId;
        options.WebSocketUri = requestDto.WebsocketUri;
      },
      options =>
      {
        options.InstanceId = _desktopClientOptions.CurrentValue.InstanceId;
      });

    if (OperatingSystem.IsWindowsVersionAtLeast(8))
    {
      builder.AddWindowsDesktopServices(requestDto.DataFolder);
    }
    else if (OperatingSystem.IsMacOS())
    {
      builder.AddMacDesktopServices(requestDto.DataFolder);
    }
    else if (OperatingSystem.IsLinux())
    {
      builder.AddLinuxDesktopServices(requestDto.DataFolder);
      builder.Services.AddSingleton<IClipboardManager, ClipboardManagerAvalonia>();
    }
    else
    {
      throw new PlatformNotSupportedException("This platform is not supported. Supported platforms are Windows, MacOS, and Linux.");
    }
    return builder;
  }

  private RemoteControlSession CreateRemoteControlSession(
    RemoteControlRequestIpcDto requestDto,
    IHost host)
  {
    var session = new RemoteControlSession(requestDto, host);
    return _sessions.AddOrUpdate(requestDto.SessionId, session, (_, value) =>
    {
      value.DisposeAsync().Forget();
      return session;
    });
  }

  private async Task HandleApplicationStopped(RemoteControlRequestIpcDto requestDto)
  {
    try
    {
      if (_sessions.TryRemove(requestDto.SessionId, out var session))
      {
        await session.DisposeAsync();
      }

      _logger.LogInformation(
        "Remote control session finished. Session ID: {SessionId}, Viewer Connection ID: {ViewerConnectionId}, " +
        "Target System Session: {TargetSystemSession}, Process ID: {TargetProcessId}, Viewer Name: {ViewerName}",
        requestDto.SessionId,
        requestDto.ViewerConnectionId,
        requestDto.TargetSystemSession,
        requestDto.TargetProcessId,
        requestDto.ViewerName);

      GC.Collect();
      GC.WaitForPendingFinalizers();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during remote control session shutdown.");
    }
  }

  private void RegisterHostLifetimeHandlers(IHost app, RemoteControlRequestIpcDto requestDto, RemoteControlSession session)
  {
    var appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    appLifetime.ApplicationStopping.Register(async () =>
    {
      await session.CancellationTokenSource.CancelAsync();
      await app.StopAsync();
    });

    appLifetime.ApplicationStopped.Register(async () =>
    {
      await HandleApplicationStopped(requestDto);
    });
  }
}