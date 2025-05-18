using ControlR.Libraries.Shared.Extensions;
using Microsoft.Extensions.Hosting;

namespace ControlR.Tests.TestingUtilities;
public sealed class FakeHostApplicationLifetime : IHostApplicationLifetime, IDisposable
{
  private readonly TimeSpan _stopDelay;
  private readonly TimeProvider _timeProvider;

  public FakeHostApplicationLifetime(
    TimeProvider timeProvider,
    bool startApplication = true,
    TimeSpan? stopDelay = null)
  {
    _timeProvider = timeProvider;

    if (startApplication)
    {
      ApplicationStartedSource.Cancel();
    }

    stopDelay ??= TimeSpan.FromSeconds(2);
    _stopDelay = stopDelay.Value;
  }

  public CancellationToken ApplicationStarted => ApplicationStartedSource.Token;
  public CancellationTokenSource ApplicationStartedSource { get; } = new();
  public CancellationToken ApplicationStopped => ApplicationStoppedSource.Token;
  public CancellationTokenSource ApplicationStoppedSource { get; } = new();
  public CancellationToken ApplicationStopping => ApplicationStoppingSource.Token;
  public CancellationTokenSource ApplicationStoppingSource { get; } = new();
  public void Dispose()
  {
    ApplicationStartedSource.Dispose();
    ApplicationStoppingSource.Dispose();
    ApplicationStoppedSource.Dispose();
  }

  public void StopApplication()
  {
    ApplicationStoppingSource.Cancel();
    Task.Run(
      async () =>
      {
        await Task.Delay(_stopDelay, _timeProvider, CancellationToken.None);
        ApplicationStoppedSource.Cancel();
      })
     .Forget();
  }
}
