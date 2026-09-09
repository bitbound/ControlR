namespace ControlR.ApiClient;

/// <summary>
/// Counts the requests a tracked target has in flight so that teardown can release the target's
/// HTTP stack after they finish instead of cancelling them.
/// </summary>
/// <remarks>
/// Disposing an <see cref="HttpClient"/> aborts the buffered requests it still has in flight, so an
/// eviction that disposes immediately turns a healthy server into a spurious failure for whoever
/// issued the call. A request announces itself with <see cref="TryEnter"/> and clears itself with
/// <see cref="Exit"/>. Whichever side of teardown observes the other performs the release, exactly
/// once.
/// </remarks>
internal sealed class InFlightTracker
{
  private readonly Lock _gate = new();

  private int _inFlight;
  private bool _released;
  private Action? _releaseHttpStack;
  private bool _teardownRequested;

  /// <summary>
  /// Announces a call for the duration of the returned lease. Dispose the lease when the caller is
  /// done with the response, including any content read from it. <see cref="Lease.Acquired"/>
  /// reports whether the call may proceed.
  /// </summary>
  public Lease Acquire() => new(this, TryEnter());

  /// <summary>
  /// Same as <see cref="Acquire"/>, for a caller that has no failed result to report and would
  /// otherwise have to repeat the refusal.
  /// </summary>
  /// <exception cref="ObjectDisposedException">The target was removed or is being removed.</exception>
  public Lease AcquireOrThrow(string ownerName)
  {
    var lease = Acquire();

    if (lease.Acquired)
    {
      return lease;
    }

    lease.Dispose();
    throw new ObjectDisposedException(ownerName, ControlrApi.DisposedTargetReason);
  }

  /// <summary>
  /// Assigns the action that releases the target's HTTP stack.
  /// </summary>
  public void AttachRelease(Action releaseHttpStack)
  {
    lock (_gate)
    {
      _releaseHttpStack = releaseHttpStack;
    }
  }

  /// <summary>
  /// Clears a previously announced request, releasing the HTTP stack if teardown was waiting on it.
  /// </summary>
  public void Exit()
  {
    lock (_gate)
    {
      _inFlight--;
    }

    TryRelease();
  }

  /// <summary>
  /// Marks teardown requested, releasing the HTTP stack now if nothing is in flight. Otherwise the
  /// last outstanding <see cref="Exit"/> does it.
  /// </summary>
  public void RequestTeardown()
  {
    lock (_gate)
    {
      _teardownRequested = true;
    }

    TryRelease();
  }

  /// <summary>
  /// Announces a request about to be sent. Returns <see langword="false"/> when teardown has
  /// already begun, in which case the request must not be sent.
  /// </summary>
  public bool TryEnter()
  {
    lock (_gate)
    {
      if (_teardownRequested)
      {
        return false;
      }

      _inFlight++;
      return true;
    }
  }

  private void TryRelease()
  {
    Action? release = null;

    lock (_gate)
    {
      if (_teardownRequested && _inFlight == 0 && !_released)
      {
        _released = true;
        release = _releaseHttpStack;
      }
    }

    // Outside the gate: releasing disposes the HTTP stack, which must not run while holding a lock
    // that an announced request is waiting to enter.
    release?.Invoke();
  }

  /// <summary>
  /// A one-shot announcement. <see cref="Acquired"/> reports whether the call may proceed, and
  /// disposing the lease clears the announcement.
  /// </summary>
  public readonly struct Lease(InFlightTracker tracker, bool acquired) : IDisposable
  {
    private readonly InFlightTracker? _tracker = acquired ? tracker : null;

    public bool Acquired => acquired;

    public void Dispose() => _tracker?.Exit();
  }
}
