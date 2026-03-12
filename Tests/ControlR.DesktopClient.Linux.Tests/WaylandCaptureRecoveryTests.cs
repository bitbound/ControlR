using ControlR.DesktopClient.Linux.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlR.DesktopClient.Linux.Tests;

public class WaylandCaptureRecoveryTests
{
  [Fact]
  public async Task CreatePipeWireStreams_ForwardsForcePortalReinitializeFlag()
  {
    var portal = new FakeXdgDesktopPortal();
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
}