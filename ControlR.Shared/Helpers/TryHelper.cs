namespace ControlR.Shared.Helpers;
public static class TryHelper
{
    public static async Task Retry(Func<Task> func, int tryCount, TimeSpan retryDelay)
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
                await Task.Delay(retryDelay);
            }
        }
    }

    public static async Task<T> Retry<T>(Func<Task<T>> func, int tryCount, TimeSpan retryDelay)
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
                await Task.Delay(retryDelay);
            }
        }
        throw new InvalidOperationException("Retry should not have reached this point.");
    }

    public static void Retry(Action action, int tryCount, TimeSpan retryDelay)
    {
        for (var i = 0; i <= tryCount; i++)
        {
            try
            {
                action.Invoke();
                return;
            }
            catch
            {
                if (i == tryCount)
                {
                    throw;
                }
                Thread.Sleep(retryDelay);
            }
        }
    }
}
