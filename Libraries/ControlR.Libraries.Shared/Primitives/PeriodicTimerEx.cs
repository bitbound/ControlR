namespace ControlR.Libraries.Shared.Primitives;

public sealed class PeriodicTimerEx(TimeSpan interval, TimeProvider timeProvider) : IDisposable
{
  private readonly PeriodicTimer _timer = new(interval, timeProvider);
  private bool _disposedValue;

  public void Dispose()
  {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }

  public async Task<bool> WaitForNextTick(bool throwOnCancellation, CancellationToken cancellationToken)
  {
    try
    {
      return await _timer.WaitForNextTickAsync(cancellationToken);

    }
    catch (OperationCanceledException) when (!throwOnCancellation)
    {
      return false;
    }
  }

  private void Dispose(bool disposing)
  {
    if (!_disposedValue)
    {
      if (disposing)
      {
        _timer.Dispose();
      }

      _disposedValue = true;
    }
  }
}