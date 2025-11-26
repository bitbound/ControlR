using System.Collections.Concurrent;
using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.ViewModels;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Services;

public interface IRemoteControlHostManager
{
  Task StartHost(RemoteControlRequestIpcDto requestDto);
  Task StopAllHosts(string reason);
  Task StopHost(Guid sessionId);
}

public class RemoteControlHostManager(
  IUserInteractionService userInteractionService,
  IOptionsMonitor<DesktopClientOptions> desktopClientOptions,
  ILogger<RemoteControlHostManager> logger) : IRemoteControlHostManager
{
  private readonly IOptionsMonitor<DesktopClientOptions> _desktopClientOptions = desktopClientOptions;
  private readonly ILogger<RemoteControlHostManager> _logger = logger;
  private readonly ConcurrentDictionary<Guid, RemoteControlSession> _sessions = new();
  private readonly IUserInteractionService _userInteractionService = userInteractionService;

  public async Task StartHost(RemoteControlRequestIpcDto requestDto)
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

      var builder = Host.CreateApplicationBuilder();
      builder.AddCommonDesktopServices(
        options =>
        {
          options.WebSocketUri = requestDto.WebsocketUri;
          options.SessionId = requestDto.SessionId;
          options.NotifyUser = requestDto.NotifyUserOnSessionStart;
          options.RequireConsent = requestDto.RequireConsent;
          options.ViewerName = requestDto.ViewerName;
        });

      builder.Services.Configure<DesktopClientOptions>(options =>
      {
        options.InstanceId = _desktopClientOptions.CurrentValue.InstanceId;
      });

      builder.Services.AddSingleton<IToaster, Toaster>();
      builder.Services.AddSingleton(_userInteractionService);
      builder.Services.AddTransient<IToastWindowViewModel, ToastWindowViewModel>();
#if WINDOWS_BUILD
      builder.AddWindowsDesktopServices(requestDto.DataFolder);
#elif MAC_BUILD
      builder.AddMacDesktopServices(requestDto.DataFolder);
#elif LINUX_BUILD
      builder.AddLinuxDesktopServices(requestDto.DataFolder);
      builder.Services.AddSingleton<IClipboardManager, ClipboardManagerAvalonia>();
#else
      throw new PlatformNotSupportedException("This platform is not supported. Supported platforms are Windows, MacOS, and Linux.");
#endif

      using var app = builder.Build();
      await using var session = CreateRemoteControlSession(requestDto);
      await app.RunAsync(session.CancellationTokenSource.Token);

      _logger.LogInformation(
        "Remote control session finished. Session ID: {SessionId}, Viewer Connection ID: {ViewerConnectionId}, " +
        "Target System Session: {TargetSystemSession}, Process ID: {TargetProcessId}, Viewer Name: {ViewerName}",
        requestDto.SessionId,
        requestDto.ViewerConnectionId,
        requestDto.TargetSystemSession,
        requestDto.TargetProcessId,
        requestDto.ViewerName);

      _ = _sessions.TryRemove(requestDto.SessionId, out _);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling remote control request.");
    }
    finally
    {
      GC.Collect();
      GC.WaitForPendingFinalizers();
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

  private RemoteControlSession CreateRemoteControlSession(RemoteControlRequestIpcDto requestDto)
  {
    var session = new RemoteControlSession();
    return _sessions.AddOrUpdate(requestDto.SessionId, session, (_, value) =>
    {
      value.DisposeAsync().Forget();
      return session;
    });
  }

  private class RemoteControlSession : IAsyncDisposable
  {
    public CancellationTokenSource CancellationTokenSource { get; } = new();

    public async ValueTask DisposeAsync()
    {
      try
      {
        await CancellationTokenSource.CancelAsync();
        CancellationTokenSource.Dispose();
      }
      catch
      {
        // Ignore.
      }
    }
  }

  private class OptionsMonitorWrapper<T>(T currentValue) : IOptionsMonitor<T>
  {
    public T CurrentValue => currentValue;
    public T Get(string? name) => currentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
  }
}