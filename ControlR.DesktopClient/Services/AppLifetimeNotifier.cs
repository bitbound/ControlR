using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.Hosting;

namespace ControlR.DesktopClient.Services;
public interface IAppLifetimeNotifier : IHostedService
{
  CancellationToken ApplicationStarted { get; }
  CancellationToken ApplicationStopping { get; }
}
internal class AppLifetimeNotifier(IControlledApplicationLifetime controlledLifetime) : IAppLifetimeNotifier
{
  private readonly CancellationTokenSource _applicationStartedSource = new();
  private readonly CancellationTokenSource _applicationStoppingSource = new();
  public CancellationToken ApplicationStarted => _applicationStartedSource.Token;
  public CancellationToken ApplicationStopping => _applicationStoppingSource.Token;
  public Task StartAsync(CancellationToken cancellationToken)
  {
    controlledLifetime.Exit += HandleApplicationExit;
    controlledLifetime.Startup += HandleApplicationStartup;
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
  }

  private void HandleApplicationExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
  {
    _applicationStoppingSource.Cancel();
  }

  private void HandleApplicationStartup(object? sender, ControlledApplicationLifetimeStartupEventArgs e)
  {
    _applicationStartedSource.Cancel();
  }
}
