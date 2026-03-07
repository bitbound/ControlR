using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Models;
using ControlR.Libraries.Shared.Collections;
using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.DesktopClient.Services;

/// <summary>
///  Manages the <see cref="IHost"/> instances of active remote control sessions.
/// </summary>
public interface IRemoteControlHostManager
{
  ICollection<RemoteControlSession> GetAllSessions();
  IDisposable OnSessionsChanged(object subscriber, Func<ICollection<RemoteControlSession>, Task> handler);
  Task<Result<RemoteControlSession>> StartHost(RemoteControlRequestIpcDto requestDto);
  Task StopAllHosts(string reason);
  Task StopHost(Guid sessionId);
  bool TryGetSession(Guid sessionId, [NotNullWhen(true)] out RemoteControlSession? session);
}

public class RemoteControlHostManager : IRemoteControlHostManager
{
  private readonly IAppLifetimeNotifier _appLifetimeNotifier;
  private readonly IOptionsMonitor<DesktopClientOptions> _desktopClientOptions;
  private readonly IIpcClientAccessor _ipcClientAccessor;
  private readonly ILogger<RemoteControlHostManager> _logger;
  private readonly HandlerCollection<ICollection<RemoteControlSession>> _sessionChangedHandlers;
  private readonly ConcurrentDictionary<Guid, RemoteControlSession> _sessions = new();
  private readonly TimeProvider _timeProvider;
  private readonly IUserInteractionService _userInteractionService;

  public RemoteControlHostManager(
    TimeProvider timeProvider,
    IUserInteractionService userInteractionService,
    IIpcClientAccessor ipcClientAccessor,
    IAppLifetimeNotifier appLifetimeNotifier,
    IOptionsMonitor<DesktopClientOptions> desktopClientOptions,
    ILogger<RemoteControlHostManager> logger)
  {
    _appLifetimeNotifier = appLifetimeNotifier;
    _desktopClientOptions = desktopClientOptions;
    _ipcClientAccessor = ipcClientAccessor;
    _logger = logger;
    _timeProvider = timeProvider;
    _userInteractionService = userInteractionService;

    _sessionChangedHandlers = new(exceptionHandler: ex =>
    {
      logger.LogError(ex, "Error while invoking session changed handlers.");
      return Task.CompletedTask;
    });

    appLifetimeNotifier.ApplicationStopping.Register(HandleParentApplicationStopping);
  }

  public ICollection<RemoteControlSession> GetAllSessions() => _sessions.Values;

  public IDisposable OnSessionsChanged(object subscriber, Func<ICollection<RemoteControlSession>, Task> handler)
  {
    return _sessionChangedHandlers.AddHandler(subscriber, handler);
  }

  public async Task<Result<RemoteControlSession>> StartHost(RemoteControlRequestIpcDto requestDto)
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
      RegisterHostLifetimeHandlers(app, requestDto);
      await app.StartAsync();
      return Result.Ok(session);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling remote control request.");
      return Result.Fail<RemoteControlSession>(ex, "An error occurred while starting the remote control session.");
    }
  }

  public async Task StopAllHosts(string reason)
  {
    try
    {
      var stopTasks = _sessions.Values.Select(async x =>
      {
        try
        {
          await x.Host.StopAsync();
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error while stopping remote control session. Session ID: {SessionId}", x.RequestDto.SessionId);
        }
      });
      await Task.WhenAll(stopTasks);
      _sessions.Clear();
      
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while stopping all remote control sessions. Reason: {Reason}", reason);
    }
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    await _sessionChangedHandlers.InvokeHandlers(_sessions.Values, cts.Token);
  }

  public async Task StopHost(Guid sessionId)
  {
    if (!_sessions.TryGetValue(sessionId, out var session))
    {
      _logger.LogWarning("No remote control session found for Session ID: {SessionId}", sessionId);
      return;
    }
    _logger.LogInformation("Stopping remote control session for Session ID: {SessionId}", sessionId);
    await session.Host.StopAsync();
  }

  public bool TryGetSession(Guid sessionId, [NotNullWhen(true)] out RemoteControlSession? session)
  {
    return _sessions.TryGetValue(sessionId, out session);
  }

  internal HostApplicationBuilder CreateRemoteControlHostBuilder(RemoteControlRequestIpcDto requestDto)
  {
    var builder = Host.CreateApplicationBuilder();

    builder.AddCommonDesktopServices(
       appBuilder =>
      {
        appBuilder.Services
          .AddSingleton<IToaster, Toaster>()
          .AddSingleton<IUiThread, UiThread>()
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

#if IS_WINDOWS
      builder.AddWindowsDesktopServices(requestDto.DataFolder);
#elif IS_MACOS
      builder.AddMacDesktopServices(requestDto.DataFolder);
#elif IS_LINUX
      builder.AddLinuxDesktopServices(requestDto.DataFolder);
      builder.Services.AddSingleton<IClipboardManager, ClipboardManagerAvalonia>();
#else
      throw new PlatformNotSupportedException("This platform is not supported. Supported platforms are Windows, MacOS, and Linux.");
#endif

    return builder;
  }

  private RemoteControlSession CreateRemoteControlSession(
    RemoteControlRequestIpcDto requestDto,
    IHost host)
  {
    var connectedAt = _timeProvider.GetLocalNow();
    var session = new RemoteControlSession(requestDto, host, connectedAt);
    var sessionResult = _sessions.AddOrUpdate(requestDto.SessionId, session, (_, value) =>
    {
      value.DisposeAsync().Forget();
      return session;
    });

    _sessionChangedHandlers
      .InvokeHandlers(_sessions.Values, _appLifetimeNotifier.ApplicationStopping)
      .Forget();

    return sessionResult;
  }

  private void HandleParentApplicationStopping()
  {
    StopAllHosts("Parent application is shutting down.").Forget();
  }

  private void RegisterHostLifetimeHandlers(IHost app, RemoteControlRequestIpcDto requestDto)
  {
    var appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    appLifetime.ApplicationStopping.Register(async () =>
    {
      await app.StopAsync();
    });

    appLifetime.ApplicationStopped.Register(async () =>
    {
      try
      {
        if (_sessions.TryRemove(requestDto.SessionId, out var session))
        {
          await session.DisposeAsync();
          await _sessionChangedHandlers.InvokeHandlers(_sessions.Values, _appLifetimeNotifier.ApplicationStopping);
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
    });
  }
}