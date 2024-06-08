using System.Diagnostics;

namespace ControlR.Libraries.Shared.Services;

public interface IDelayer
{
    Task Delay(TimeSpan delay, CancellationToken cancellationToken = default);
    Task<bool> WaitForAsync(Func<bool> condition, TimeSpan timeout, int pollingMs = 10);
}

public class Delayer : IDelayer
{
    public static Delayer Default { get; } = new();
    public async Task<bool> WaitForAsync(Func<bool> condition, TimeSpan timeout, int pollingMs = 10)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.Elapsed < timeout)
        {
            await Task.Delay(pollingMs);
        }
        return condition();
    }

    public async Task Delay(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        await Task.Delay(delay, cancellationToken);
    }
}
