using ControlR.DesktopClient.Linux.XdgPortal;
using Microsoft.Win32.SafeHandles;

namespace ControlR.DesktopClient.Linux.Tests;

internal sealed class FakeXdgDesktopPortal : IXdgDesktopPortal
{
  public List<bool> InitializeCalls { get; } = [];
  public List<(string session, int keycode, bool pressed)> KeyboardCalls { get; } = [];
  public string? SessionHandle { get; set; } = "fake-session";
  public (SafeFileHandle Fd, string SessionHandle)? PipeWireConnectionResult { get; set; }

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

  public Task Initialize(bool forceReinitialization = false, bool bypassRestoreToken = false)
  {
    InitializeCalls.Add(forceReinitialization);
    return Task.CompletedTask;
  }

  public Task NotifyKeyboardKeycodeAsync(string sessionHandle, int keycode, bool pressed)
  {
    KeyboardCalls.Add((sessionHandle, keycode, pressed));
    return Task.CompletedTask;
  }

  public Task NotifyKeyboardKeysymAsync(string sessionHandle, int keysym, bool pressed)
  {
    return Task.CompletedTask;
  }

  public Task NotifyPointerAxisAsync(string sessionHandle, double dx, double dy, bool finish = true)
  {
    return Task.CompletedTask;
  }

  public Task NotifyPointerAxisDiscreteAsync(string sessionHandle, uint axis, int steps)
  {
    return Task.CompletedTask;
  }

  public Task NotifyPointerButtonAsync(string sessionHandle, int button, bool pressed)
  {
    return Task.CompletedTask;
  }

  public Task NotifyPointerMotionAbsoluteAsync(string sessionHandle, uint stream, double x, double y)
  {
    return Task.CompletedTask;
  }

  public Task NotifyPointerMotionAsync(string sessionHandle, double dx, double dy)
  {
    return Task.CompletedTask;
  }
}