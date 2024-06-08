namespace ControlR.Libraries.Shared.Services;

public interface IRetryer
{
    Task Retry(Func<Task> func, int tryCount, TimeSpan retryDelay);
    Task<T> Retry<T>(Func<Task<T>> func, int tryCount, TimeSpan retryDelay);
}

public class Retryer : IRetryer
{
    public static Retryer Default { get; } = new();

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
                await Task.Delay(retryDelay);
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
                await Task.Delay(retryDelay);
            }
        }
        throw new InvalidOperationException("Retry should not have reached this point.");
    }
}
