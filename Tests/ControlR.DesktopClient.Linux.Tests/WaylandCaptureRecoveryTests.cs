using System.Drawing;
using ControlR.DesktopClient.Linux.Services;
using ControlR.DesktopClient.Linux.XdgPortal;
using ControlR.Libraries.NativeInterop.Linux;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace ControlR.DesktopClient.Linux.Tests;

public class WaylandCaptureRecoveryTests
{
  [Fact]
  public async Task CreatePipeWireStreams_ForwardsForcePortalReinitializeFlag()
  {
    var portal = new TrackingPortal();
    using var displayManager = new DisplayManagerWayland(
      TimeProvider.System,
      portal,
      streamFactory: null!,
      NullLogger<DisplayManagerWayland>.Instance);

    _ = await displayManager.CreatePipeWireStreams(
      forcePortalReinitialize: true,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal([true], portal.InitializeCalls);
  }

  [Fact]
  public void ShouldRecoverStream_ReturnsFalse_WhenFrameIsFresh()
  {
    var nowUtc = new DateTime(2026, 3, 11, 21, 0, 0, DateTimeKind.Utc);
    var createdUtc = nowUtc.AddSeconds(-10);
    var lastFrameReceivedUtc = nowUtc.AddMilliseconds(-500);

    var shouldRecover = ScreenGrabberWayland.ShouldRecoverStream(
      nowUtc,
      createdUtc,
      lastFrameReceivedUtc,
      staleThreshold: TimeSpan.FromSeconds(2),
      lastRecoveryAttemptUtc: null,
      recoveryCooldown: TimeSpan.FromSeconds(2));

    Assert.False(shouldRecover);
  }

  [Fact]
  public void ShouldRecoverStream_ReturnsFalse_WhenRecoveryCooldownIsActive()
  {
    var nowUtc = new DateTime(2026, 3, 11, 21, 0, 0, DateTimeKind.Utc);
    var createdUtc = nowUtc.AddSeconds(-10);
    var lastFrameReceivedUtc = nowUtc.AddSeconds(-5);
    var lastRecoveryAttemptUtc = nowUtc.AddMilliseconds(-500);

    var shouldRecover = ScreenGrabberWayland.ShouldRecoverStream(
      nowUtc,
      createdUtc,
      lastFrameReceivedUtc,
      staleThreshold: TimeSpan.FromSeconds(2),
      lastRecoveryAttemptUtc,
      recoveryCooldown: TimeSpan.FromSeconds(2));

    Assert.False(shouldRecover);
  }

  [Fact]
  public void ShouldRecoverStream_ReturnsTrue_WhenLatestFrameIsStale()
  {
    var nowUtc = new DateTime(2026, 3, 11, 21, 0, 0, DateTimeKind.Utc);
    var createdUtc = nowUtc.AddSeconds(-10);
    var lastFrameReceivedUtc = nowUtc.AddSeconds(-5);

    var shouldRecover = ScreenGrabberWayland.ShouldRecoverStream(
      nowUtc,
      createdUtc,
      lastFrameReceivedUtc,
      staleThreshold: TimeSpan.FromSeconds(2),
      lastRecoveryAttemptUtc: null,
      recoveryCooldown: TimeSpan.FromSeconds(2));

    Assert.True(shouldRecover);
  }

  private sealed class TrackingPortal : IXdgDesktopPortal
  {
    public List<bool> InitializeCalls { get; } = [];

    public void Dispose()
    {
    }

    public Task<(SafeFileHandle Fd, string SessionHandle)?> GetPipeWireConnection()
    {
      return Task.FromResult<(SafeFileHandle, string)?>(null);
    }

    public Task<string?> GetRemoteDesktopSessionHandle()
    {
      return Task.FromResult<string?>(null);
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
}