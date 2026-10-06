using System.Collections.Concurrent;

namespace ControlR.Libraries.Shared.Services.Locks;

/// <summary>
/// In-process keyed lock backed by a per-key semaphore. Idle entries are evicted as soon as
/// no holder or waiter remains, so distinct keys never accumulate. Suitable only while the
/// server runs as a single instance; to scale horizontally, swap the registration for a
/// distributed <see cref="IAsyncLock"/> implementation (e.g. Redis).
/// </summary>
public sealed class KeyedLock : IAsyncLock
{
  private readonly ConcurrentDictionary<string, Gate> _gates = [];
  private readonly Lock _sync = new();

  public async Task<IAsyncDisposable> AcquireAsync(string key, CancellationToken cancellationToken)
  {
    Gate gate;
    lock (_sync)
    {
      gate = _gates.GetOrAdd(key, static k => new Gate(k));
      gate.ParticipantCount++;
    }

    try
    {
      await gate.Semaphore.WaitAsync(cancellationToken);
    }
    catch
    {
      ReleaseCore(gate, releaseSemaphore: false);
      throw;
    }

    return new Releaser(this, gate);
  }

  private void Release(Gate gate) => ReleaseCore(gate, releaseSemaphore: true);

  /// <summary>
  /// Releases (optionally) the gate's semaphore, then removes the gate from the dictionary
  /// once no holders or waiters remain. Both the register path and this path take
  /// <see cref="_sync"/>, so a gate can never be evicted while a thread that already fetched
  /// it is still registering or waiting on it.
  /// </summary>
  private void ReleaseCore(Gate gate, bool releaseSemaphore)
  {
    lock (_sync)
    {
      if (releaseSemaphore)
      {
        gate.Semaphore.Release();
      }

      gate.ParticipantCount--;

      if (gate.ParticipantCount == 0 &&
        _gates.TryGetValue(gate.Key, out var current) &&
        ReferenceEquals(current, gate))
      {
        _gates.TryRemove(new KeyValuePair<string, Gate>(gate.Key, gate));
      }
    }
  }

  private sealed class Gate(string key)
  {
    internal int ParticipantCount;

    internal string Key { get; } = key;
    internal SemaphoreSlim Semaphore { get; } = new(1, 1);
  }

  private sealed class Releaser(KeyedLock owner, Gate gate) : IAsyncDisposable
  {
    private int _disposed;

    public ValueTask DisposeAsync()
    {
      if (Interlocked.Exchange(ref _disposed, 1) == 0)
      {
        owner.Release(gate);
      }
      return ValueTask.CompletedTask;
    }
  }
}
