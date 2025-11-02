namespace ControlR.Libraries.Shared.Extensions;

public static class TimerExtensions
{
  public static async Task<bool> WaitForNextTick(
      this PeriodicTimer timer,
      bool throwOnCancellation,
      CancellationToken cancellationToken)
  {
    try
    {
      return await timer.WaitForNextTickAsync(cancellationToken);
    }
    catch (OperationCanceledException) when (!throwOnCancellation)
    {
      return false;
    }
  }
}