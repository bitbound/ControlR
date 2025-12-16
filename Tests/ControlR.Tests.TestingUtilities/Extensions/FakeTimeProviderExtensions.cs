using Microsoft.Extensions.Time.Testing;
using System.Collections;
using System.Reflection;

namespace ControlR.Tests.TestingUtilities.Extensions;

public static class FakeTimeProviderExtensions
{
  public static async Task<bool> WaitForWaiters(
    this FakeTimeProvider timeProvider,
    Func<int, bool> condition,
    TimeSpan? pollingDelay = null,
    CancellationToken cancellationToken = default)
  {
    pollingDelay ??= TimeSpan.FromMilliseconds(50);

    var waiters = (typeof(FakeTimeProvider)
      .GetField("Waiters", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField)
      ?.GetValue(timeProvider)) as IEnumerable ??
      throw new InvalidOperationException("Failed to retrieve waiters.");

    while (!cancellationToken.IsCancellationRequested)
    {
      var count = waiters
        .Cast<object>()
        .Count();

      if (condition.Invoke(count))
        return true;
      
      await Task.Delay(pollingDelay.Value, cancellationToken);
    }

    return false;
  }
}
