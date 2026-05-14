using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.Hosting;

namespace ControlR.DesktopClient.Services;

internal sealed class DesktopApplicationLifetime(IControlledApplicationLifetime controlledLifetime)
  : IHostApplicationLifetime, IHostedService
{
  private readonly CancellationTokenSource _applicationStartedSource = new();
  private readonly CancellationTokenSource _applicationStoppedSource = new();
  private readonly CancellationTokenSource _applicationStoppingSource = new();

  public CancellationToken ApplicationStarted => _applicationStartedSource.Token;
  public CancellationTokenSource ApplicationStartedSource => _applicationStartedSource;
  public CancellationToken ApplicationStopped => _applicationStoppedSource.Token;
  public CancellationTokenSource ApplicationStoppedSource => _applicationStoppedSource;
  public CancellationToken ApplicationStopping => _applicationStoppingSource.Token;
  public CancellationTokenSource ApplicationStoppingSource => _applicationStoppingSource;

  public Task StartAsync(CancellationToken cancellationToken)
  {
    controlledLifetime.Exit += HandleApplicationExit;
    controlledLifetime.Startup += HandleApplicationStartup;
    return Task.CompletedTask;
  }

  public void StopApplication()
  {
    controlledLifetime.Shutdown();
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    StopApplication();
    return Task.CompletedTask;
  }

  private void HandleApplicationExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
  {
    _applicationStoppingSource.Cancel();
    _applicationStoppedSource.Cancel();
  }

  private void HandleApplicationStartup(object? sender, ControlledApplicationLifetimeStartupEventArgs e)
  {
    _applicationStartedSource.Cancel();
  }
}
