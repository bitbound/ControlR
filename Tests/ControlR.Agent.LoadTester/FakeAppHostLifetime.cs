using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.LoadTester;
internal class FakeAppHostLifetime(CancellationToken stoppingToken) : IHostApplicationLifetime
{
  private readonly static CancellationTokenSource _source = new(0);
  public CancellationToken ApplicationStarted { get; } = _source.Token;

  public CancellationToken ApplicationStopping { get; } = stoppingToken;

  public CancellationToken ApplicationStopped { get; } = stoppingToken;

  public void StopApplication()
  {

  }
}
