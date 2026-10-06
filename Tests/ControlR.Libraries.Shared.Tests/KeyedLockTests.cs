using ControlR.Libraries.Shared.Services.Locks;

namespace ControlR.Libraries.Shared.Tests;

public class KeyedLockTests
{
  [Fact]
  public async Task AcquireAsync_DifferentKeys_RunConcurrently()
  {
    var lockService = new KeyedLock();
    var overlapObserved = false;
    var active = 0;
    var lockObj = new object();
    var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    async Task Workload(string key)
    {
      await startGate.Task;
      await using var handle = await lockService.AcquireAsync(key, TestContext.Current.CancellationToken);
      var current = Interlocked.Increment(ref active);
      if (current > 1)
      {
        lock (lockObj)
        {
          overlapObserved = true;
        }
      }
      try
      {
        // Hold long enough that any same-key serialization would be visible.
        await Task.Delay(200, TestContext.Current.CancellationToken);
      }
      finally
      {
        Interlocked.Decrement(ref active);
      }
    }

    var workloads = new[]
    {
      Workload("key-a"),
      Workload("key-b"),
      Workload("key-c"),
      Workload("key-d")
    };

    // Release all workloads at the same instant so they contend together.
    startGate.SetResult();
    await Task.WhenAll(workloads);

    Assert.True(overlapObserved, "Different keys should be able to run at the same time.");
  }

  [Fact]
  public async Task AcquireAsync_KeyReusedAfterFullRelease_DoesNotBlock()
  {
    var lockService = new KeyedLock();

    // First cycle fully releases, which evicts the gate for "key".
    for (var i = 0; i < 5; i++)
    {
      await using var handle = await lockService.AcquireAsync("key", TestContext.Current.CancellationToken);
    }

    // Re-acquiring the same key after eviction must work immediately.
    await using var reacquire = await lockService.AcquireAsync("key", TestContext.Current.CancellationToken);
  }

  [Fact]
  public async Task AcquireAsync_SameKey_AcrossEvictionCycles_NeverOverlaps()
  {
    var lockService = new KeyedLock();
    var maxConcurrent = 0;
    var active = 0;
    var lockObj = new object();
    const int iterations = 200;

    // Each iteration fully releases its gate (evicting it), then the next iteration must
    // recreate it. This exercises the evict-and-recreate path and would surface a stranded
    // gate handing the lock to two holders at once.
    for (var i = 0; i < iterations; i++)
    {
      await using var handle = await lockService.AcquireAsync("key", TestContext.Current.CancellationToken);

      var current = Interlocked.Increment(ref active);
      lock (lockObj)
      {
        maxConcurrent = Math.Max(maxConcurrent, current);
      }

      await Task.Yield();

      Interlocked.Decrement(ref active);
    }

    Assert.Equal(1, maxConcurrent);
  }

  [Fact]
  public async Task AcquireAsync_SameKey_SerializesAccess()
  {
    var lockService = new KeyedLock();
    var maxConcurrent = 0;
    var active = 0;
    var lockObj = new object();

    async Task Workload()
    {
      await using var handle = await lockService.AcquireAsync("key", TestContext.Current.CancellationToken);
      var current = Interlocked.Increment(ref active);
      lock (lockObj)
      {
        maxConcurrent = Math.Max(maxConcurrent, current);
      }
      try
      {
        await Task.Delay(50, TestContext.Current.CancellationToken);
      }
      finally
      {
        Interlocked.Decrement(ref active);
      }
    }

    await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Workload()));

    Assert.Equal(1, maxConcurrent);
  }

  [Fact]
  public async Task Acquiring_ThenDisposing_ReleasesForNextAcquirer()
  {
    var lockService = new KeyedLock();

    var first = await lockService.AcquireAsync("key", TestContext.Current.CancellationToken);
    await first.DisposeAsync();

    // Should acquire immediately without blocking.
    await using var second = await lockService.AcquireAsync("key", TestContext.Current.CancellationToken);
  }

  [Fact]
  public async Task Dispose_ReleasesLock_EvenWhenHolderDoesNotFinishFirst()
  {
    var lockService = new KeyedLock();
    var releaseOrder = new List<string>();

    var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    var holder = Task.Run(async () =>
    {
      await using var handle = await lockService.AcquireAsync("key", TestContext.Current.CancellationToken);
      releaseOrder.Add("first-holds");
      acquired.SetResult();
      // Simulate long-running holder that does not complete before the second acquirer.
      await Task.Delay(200, TestContext.Current.CancellationToken);
    }, TestContext.Current.CancellationToken);

    await acquired.Task;

    var secondAcquired = false;
    var second = Task.Run(async () =>
    {
      await using var handle = await lockService.AcquireAsync("key", TestContext.Current.CancellationToken);
      releaseOrder.Add("second-acquires");
      secondAcquired = true;
    }, TestContext.Current.CancellationToken);

    await second;

    Assert.True(secondAcquired, "Second acquirer should obtain the lock only after the first releases it.");
    Assert.Equal(["first-holds", "second-acquires"], releaseOrder);
    await holder;
  }

  [Fact]
  public async Task Dispose_WhenCalledTwice_ReleasesOnlyOnce()
  {
    var lockService = new KeyedLock();

    var handle = await lockService.AcquireAsync("key", TestContext.Current.CancellationToken);

    // First disposal releases the lock.
    await handle.DisposeAsync();
    var second = await lockService.AcquireAsync("key", TestContext.Current.CancellationToken);

    // Second disposal of the first handle must be a no-op: it must neither throw a
    // SemaphoreFullException nor evict the gate for the currently-held second lock.
    await handle.DisposeAsync();

    // Explicitly release the second holder, then re-acquire. If the second no-op dispose
    // had evicted the gate while second still held it, this would still work here, but
    // restoring the gate properly means the re-acquire must not block.
    await second.DisposeAsync();
    var third = await lockService.AcquireAsync("key", TestContext.Current.CancellationToken);
    await third.DisposeAsync();
  }
}
