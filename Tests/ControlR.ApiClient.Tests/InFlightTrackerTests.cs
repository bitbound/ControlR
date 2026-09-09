namespace ControlR.ApiClient.Tests;

public sealed class InFlightTrackerTests
{
  [Fact]
  public void Exit_ReleasesExactlyOnceWhenSeveralRequestsDrainAfterTeardown()
  {
    var released = 0;
    var tracker = new InFlightTracker();
    tracker.AttachRelease(() => released++);
    Assert.True(tracker.TryEnter());
    Assert.True(tracker.TryEnter());
    tracker.RequestTeardown();

    tracker.Exit();
    tracker.Exit();

    Assert.Equal(1, released);
  }

  [Fact]
  public void Exit_WithoutTeardown_NeverReleases()
  {
    var released = 0;
    var tracker = new InFlightTracker();
    tracker.AttachRelease(() => released++);
    Assert.True(tracker.TryEnter());

    tracker.Exit();

    Assert.Equal(0, released);
  }

  [Fact]
  public void Lease_WhenAcquired_HoldsTheCountUntilItIsDisposed()
  {
    var released = 0;
    var tracker = new InFlightTracker();
    tracker.AttachRelease(() => released++);

    using (var lease = tracker.Acquire())
    {
      Assert.True(lease.Acquired);

      tracker.RequestTeardown();

      Assert.Equal(0, released);
    }

    Assert.Equal(1, released);
  }

  [Fact]
  public void Lease_WhenRefusedByTeardown_HoldsNothingAndReleasesNothingWhenDisposed()
  {
    var released = 0;
    var tracker = new InFlightTracker();
    tracker.AttachRelease(() => released++);

    using (var outstanding = tracker.Acquire())
    {
      Assert.True(outstanding.Acquired);
      tracker.RequestTeardown();

      var refused = tracker.Acquire();

      Assert.False(refused.Acquired);
      refused.Dispose();

      // A refused lease that still cleared the announcement would drop the count to zero here and
      // release the stack that the outstanding request is still using.
      Assert.Equal(0, released);

      // Disposing twice must not clear a second announcement either.
      refused.Dispose();
      Assert.Equal(0, released);
    }

    Assert.Equal(1, released);
  }

  [Fact]
  public void RequestTeardown_WhenNothingIsInFlight_ReleasesImmediately()
  {
    var released = 0;
    var tracker = new InFlightTracker();
    tracker.AttachRelease(() => released++);

    tracker.RequestTeardown();

    Assert.Equal(1, released);
  }

  [Fact]
  public void RequestTeardown_WhenRequestIsInFlight_DelegatesReleaseToTheLastExit()
  {
    var released = 0;
    var tracker = new InFlightTracker();
    tracker.AttachRelease(() => released++);
    Assert.True(tracker.TryEnter());

    tracker.RequestTeardown();

    Assert.Equal(0, released);

    tracker.Exit();

    Assert.Equal(1, released);
  }

  [Fact]
  public void TryEnter_AfterTeardown_RefusesTheRequestAndStillReleasesWhenTheCountDrains()
  {
    var released = 0;
    var tracker = new InFlightTracker();
    tracker.AttachRelease(() => released++);
    Assert.True(tracker.TryEnter());
    tracker.RequestTeardown();

    // A request that announces itself after teardown is refused, and the refusal must not release
    // the stack that the still-outstanding first request is using.
    Assert.False(tracker.TryEnter());

    Assert.Equal(0, released);

    // The first request is the one still holding the stack.
    tracker.Exit();

    Assert.Equal(1, released);
  }
}
