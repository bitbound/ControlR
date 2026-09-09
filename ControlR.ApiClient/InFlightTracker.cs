namespace ControlR.ApiClient;

/// <summary>
/// Counts the requests a tracked target has in flight so that teardown can release the target's
/// HTTP stack after they finish instead of cancelling them.
/// </summary>
/// <remarks>
/// <para>
/// Disposing an <see cref="HttpClient"/> aborts the buffered requests it still has in flight, so an
/// eviction that disposes immediately turns a healthy server into a spurious failure for whoever
/// issued the call. A request announces itself with <see cref="TryEnter"/> and clears itself with
/// <see cref="Exit"/>; <see cref="RequestTeardown"/> leaves the release with whichever side loses
/// the race.
/// </para>
/// <para>
/// The pairing is a Dekker handshake between the teardown flag and the in-flight count: each side
/// publishes its own state, fully fenced, before reading the other's. That is what makes exactly one
/// of them observe the other, so the release can be neither lost nor run twice.
/// </para>
/// <para>
/// The publishing must be a seq_cst <see cref="Interlocked"/> operation rather than a
/// <see cref="Volatile.Write"/>. A release store followed by an acquire load orders each thread's own
/// accesses around it, but never orders the store ahead of the load: the store can stay buffered
/// locally while the load is answered from memory. Both threads doing that is the one outcome that
/// breaks the handshake, and it is permitted on the ARM hosts this library ships to.
/// </para>
/// </remarks>
internal sealed class InFlightTracker
{
  private int _inFlight;
  private int _released;
  private Action? _releaseHttpStack;
  private int _teardownRequested;

  /// <summary>
  /// Announces a call for the duration of the returned lease. Dispose the lease when the caller is
  /// done with the response, including any content read from it.
  /// </summary>
  public Lease Acquire() => new(this, TryEnter());

  /// <summary>
  /// Assigns the action that releases the target's HTTP stack. Call it once, while building the
  /// target and before its client can be reached by anyone else.
  /// </summary>
  public void AttachRelease(Action releaseHttpStack)
  {
    _releaseHttpStack = releaseHttpStack;
  }

  /// <summary>
  /// Clears a previously announced request, releasing the HTTP stack if teardown was waiting on it.
  /// </summary>
  public void Exit()
  {
    if (Interlocked.Decrement(ref _inFlight) > 0 || Volatile.Read(ref _teardownRequested) != 1)
    {
      return;
    }

    Release();
  }

  /// <summary>
  /// <para>
  /// Marks teardown requested and releases the target's HTTP stack.
  /// </para>
  /// <para>
  /// When nothing is in flight, the stack is released before this call returns. While requests are
  /// still out there, the last one's <see cref="Exit"/> does it instead.
  /// </para>
  /// </summary>
  public void RequestTeardown()
  {
    Interlocked.Exchange(ref _teardownRequested, 1);

    if (Volatile.Read(ref _inFlight) == 0)
    {
      Release();
    }
  }

  /// <summary>
  /// Announces a request about to be sent. Returns <see langword="false"/> when teardown has
  /// already begun, in which case the request must not be sent.
  /// </summary>
  public bool TryEnter()
  {
    Interlocked.Increment(ref _inFlight);

    if (Volatile.Read(ref _teardownRequested) == 1)
    {
      Exit();
      return false;
    }

    return true;
  }

  private void Release()
  {
    if (Interlocked.Exchange(ref _released, 1) == 1)
    {
      return;
    }

    _releaseHttpStack?.Invoke();
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
