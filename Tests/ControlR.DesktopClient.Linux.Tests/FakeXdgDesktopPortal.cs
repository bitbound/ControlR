using ControlR.DesktopClient.Linux.XdgPortal;
using Microsoft.Win32.SafeHandles;

namespace ControlR.DesktopClient.Linux.Tests;

internal sealed class FakeXdgDesktopPortal : IXdgDesktopPortal
{
  public int DeleteRestoreTokenCallCount { get; private set; }
  public int InitializeCallCount { get; private set; }
  public List<(string session, int keycode, bool pressed)> KeyboardCalls { get; } = [];
  public List<(string session, int keysym, bool pressed)> KeysymCalls { get; } = [];
  public (SafeFileHandle Fd, string SessionHandle)? PipeWireConnectionResult { get; set; }
  public bool ProbeResult { get; set; } = true;
  public int RequestRemoteDesktopPermissionCallCount { get; private set; }
  public bool RequestRemoteDesktopPermissionResult { get; set; } = true;
  public string? SessionHandle { get; set; } = "fake-session";

  public void DeleteRestoreToken()
  {
    DeleteRestoreTokenCallCount++;
  }

  public void Dispose()
  {
  }

  public Task<(SafeFileHandle Fd, string SessionHandle)?> GetPipeWireConnection()
  {
    return Task.FromResult(PipeWireConnectionResult);
  }

  public Task<string?> GetRemoteDesktopSessionHandle()
  {
    return Task.FromResult(SessionHandle);
  }

  public Task<List<PipeWireStreamInfo>> GetScreenCastStreams()
  {
    return Task.FromResult<List<PipeWireStreamInfo>>([]);
  }

  public Task Initialize(bool bypassRestoreToken = false)
  {
    InitializeCallCount++;
    return Task.CompletedTask;
  }

  public Task NotifyKeyboardKeycode(string sessionHandle, int keycode, bool pressed)
  {
    KeyboardCalls.Add((sessionHandle, keycode, pressed));
    return Task.CompletedTask;
  }

  public Task NotifyKeyboardKeysym(string sessionHandle, int keysym, bool pressed)
  {
    KeysymCalls.Add((sessionHandle, keysym, pressed));
    return Task.CompletedTask;
  }

  public Task NotifyPointerAxis(string sessionHandle, double dx, double dy, bool finish = true)
  {
    return Task.CompletedTask;
  }

  public Task NotifyPointerAxisDiscrete(string sessionHandle, uint axis, int steps)
  {
    return Task.CompletedTask;
  }

  public Task NotifyPointerButton(string sessionHandle, int button, bool pressed)
  {
    return Task.CompletedTask;
  }

  public Task NotifyPointerMotion(string sessionHandle, double dx, double dy)
  {
    return Task.CompletedTask;
  }

  public Task NotifyPointerMotionAbsolute(string sessionHandle, uint stream, double x, double y)
  {
    return Task.CompletedTask;
  }

  public Task<bool> ProbeRestoreToken(string restoreToken, CancellationToken cancellationToken = default)
  {
    return Task.FromResult(ProbeResult);
  }

  public Task<bool> RequestRemoteDesktopPermission(bool bypassRestoreToken = false, CancellationToken cancellationToken = default)
  {
    RequestRemoteDesktopPermissionCallCount++;
    return Task.FromResult(RequestRemoteDesktopPermissionResult);
  }
}