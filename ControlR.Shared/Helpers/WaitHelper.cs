using System.Diagnostics;

namespace ControlR.Shared.Helpers;

public static class WaitHelper
{
    public static async Task<bool> WaitForAsync(Func<bool> condition, TimeSpan timeout, int pollingMs = 10)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.Elapsed < timeout)
        {
            await Task.Delay(pollingMs);
        }
        return condition();
    }
}
