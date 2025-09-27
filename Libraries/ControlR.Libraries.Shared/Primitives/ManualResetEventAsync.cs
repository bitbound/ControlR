namespace ControlR.Libraries.Shared.Primitives;

public sealed class ManualResetEventAsync : IDisposable
{
  private readonly Lock _taskLock = new();
  private TaskCompletionSource _tcs = new();

  public ManualResetEventAsync(bool isSet = false)
  {
    if (isSet)
    {
      Set();
    }
  }

  public bool IsSet
  {
    get
    {
      lock (_taskLock)
      {
        return _tcs.Task.IsCompleted;
      }
    }
  }

  public void Dispose()
  {
    Set();
  }

  public void Reset()
  {
    lock (_taskLock)
    {
      if (!_tcs.Task.IsCompleted)
      {
        return;
      }

      _tcs = new();
    }
  }

  public void Set()
  {
    lock (_taskLock)
    {
      _ = _tcs.TrySetResult();
    }
  }

  public Task Wait(CancellationToken cancellationToken)
  {
    lock (_taskLock)
    {
      return _tcs.Task.WaitAsync(cancellationToken);
    }
  }

  public async Task<bool> Wait(TimeSpan timeout, bool throwOnCancellation)
  {
    try
    {
      await _tcs.Task.WaitAsync(timeout);
      return true;
    }
    catch (TimeoutException) when (!throwOnCancellation)
    {
      return false;
    }
  }
}