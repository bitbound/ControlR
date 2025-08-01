﻿namespace ControlR.Libraries.Shared.Services;

public interface IRetryer
{
  Task Retry(Func<Task> func, int tryCount, TimeSpan retryDelay);
  Task<T> Retry<T>(Func<Task<T>> func, int tryCount, TimeSpan retryDelay);
}

public class Retryer(TimeProvider timeProvider) : IRetryer
{
  private readonly TimeProvider _timeProvider = timeProvider;
  public static Retryer Default { get; } = new(TimeProvider.System);

  public async Task Retry(Func<Task> func, int tryCount, TimeSpan retryDelay)
  {
    for (var i = 0; i <= tryCount; i++)
    {
      try
      {
        await func.Invoke();
        return;
      }
      catch
      {
        if (i == tryCount)
        {
          throw;
        }
        await Task.Delay(retryDelay, _timeProvider);
      }
    }
  }

  public async Task<T> Retry<T>(Func<Task<T>> func, int tryCount, TimeSpan retryDelay)
  {
    for (var i = 0; i <= tryCount; i++)
    {
      try
      {
        return await func.Invoke();
      }
      catch
      {
        if (i == tryCount)
        {
          throw;
        }
        await Task.Delay(retryDelay, _timeProvider);
      }
    }
    throw new InvalidOperationException("Retry should not have reached this point.");
  }
}
