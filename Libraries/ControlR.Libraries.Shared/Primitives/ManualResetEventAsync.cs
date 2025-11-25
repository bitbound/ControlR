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
    Task task;
    lock (_taskLock)
    {
      task = _tcs.Task;
    }
    return task.WaitAsync(cancellationToken);
  }

  public async Task<bool> Wait(TimeSpan timeout, bool throwOnCancellation)
  {
    Task task;
    lock (_taskLock)
    {
      task = _tcs.Task;
    }
    try
    {
      await task.WaitAsync(timeout);
      return true;
    }
    catch (TimeoutException) when (!throwOnCancellation)
    {
      return false;
    }
  }
}