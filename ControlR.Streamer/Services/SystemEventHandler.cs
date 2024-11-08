using System.Diagnostics;
using Bitbound.SimpleMessenger;
using ControlR.Streamer.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;

namespace ControlR.Streamer.Services;

internal class SystemEventHandler(
  IMessenger messenger,
  IHostApplicationLifetime appLifetime,
  ILogger<SystemEventHandler> logger) : IHostedService
{
  public Task StartAsync(CancellationToken cancellationToken)
  {
    SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
    SystemEvents.SessionEnding += SystemEvents_SessionEnding;
    SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
    SystemEvents.SessionEnding -= SystemEvents_SessionEnding;
    SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
    return Task.CompletedTask;
  }

  private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
  {
    messenger.Send(new DisplaySettingsChangedMessage());
  }

  private async void SystemEvents_SessionEnding(object? sender, SessionEndingEventArgs e)
  {
    logger.LogInformation("Session ending.  Reason: {reason}", e.Reason);

    var reason = (SessionEndReasonsEx)e.Reason;
    await messenger.Send(new WindowsSessionEndingMessage(reason));

    appLifetime.StopApplication();
  }

  private void SystemEvents_SessionSwitch(object? sender, SessionSwitchEventArgs e)
  {
    logger.LogInformation("Session changing.  Reason: {reason}", e.Reason);

    var reason = (SessionSwitchReasonEx)(int)e.Reason;
    messenger.Send(new WindowsSessionSwitchedMessage(reason, Process.GetCurrentProcess().SessionId));

    appLifetime.StopApplication();
  }
}