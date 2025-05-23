﻿namespace ControlR.Libraries.Shared.Primitives;

/// <summary>
///   A wrapper around two <see cref="SemaphoreSlim" />s.  When awaited, the
///   outer lock will be tested first and return immediately if the semaphore's
///   count has reached 0 (i.e. spills over the funnel).  The inner lock will
///   wait until cancelled or timed out.
/// </summary>
/// <param name="outerInitialCount"></param>
/// <param name="outerMaxCount"></param>
/// <param name="innerInitialCount"></param>
/// <param name="innerMaxCount"></param>
public class FunnelLock(
  int outerInitialCount,
  int outerMaxCount,
  int innerInitialCount,
  int innerMaxCount)
{
  private readonly SemaphoreSlim _innerLock = new(innerInitialCount, innerMaxCount);
  private readonly SemaphoreSlim _outerLock = new(outerInitialCount, outerMaxCount);

  public async Task<DisposableValue<bool>> WaitAsync(CancellationToken cancellationToken)
  {
    if (!await _outerLock.WaitAsync(0, cancellationToken))
    {
      return new DisposableValue<bool>(false);
    }

    await _innerLock.WaitAsync(cancellationToken);
    return new DisposableValue<bool>(true, Release);
  }

  public async Task<DisposableValue<bool>> WaitAsync(TimeSpan timeout)
  {
    if (!await _outerLock.WaitAsync(0))
    {
      return new DisposableValue<bool>(false);
    }

    var waitResult = await _innerLock.WaitAsync(timeout);
    return waitResult ? new DisposableValue<bool>(true, Release) : new DisposableValue<bool>(false);
  }

  public void Release()
  {
    _outerLock.Release();
    _innerLock.Release();
  }
}