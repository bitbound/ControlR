using System.Diagnostics;
using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace ControlR.DesktopClient.Windows.Services;

internal class SystemEventHandler(
  IMessenger messenger,
  IHostApplicationLifetime appLifetime,
  ILogger<SystemEventHandler> logger) : IHostedService
{
  public Task StartAsync(CancellationToken cancellationToken)
  {
    SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
    SystemEvents.SessionEnding += SystemEvents_SessionEnding;
    SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
    SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
    SystemEvents.SessionEnding -= SystemEvents_SessionEnding;
    SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
    SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
    return Task.CompletedTask;
  }

  private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
  {
    messenger.Send(new DisplaySettingsChangedMessage());
  }

  private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
  {
    if (e.Mode == PowerModes.Suspend)
    {
      _ = messenger.Send(new WindowsSessionEndingMessage(SessionEndReasonsEx.SuspendMode));
    }
  }

  private async void SystemEvents_SessionEnding(object? sender, SessionEndingEventArgs e)
  {
    try
    {
      logger.LogInformation("Session ending.  Reason: {reason}", e.Reason);

      var reason = (SessionEndReasonsEx)e.Reason;
      await messenger.Send(new WindowsSessionEndingMessage(reason));

      appLifetime.StopApplication();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error ending session");
    }
  }

  private void SystemEvents_SessionSwitch(object? sender, SessionSwitchEventArgs e)
  {
    try
    {
      logger.LogInformation("Session changing.  Reason: {reason}", e.Reason);

      var reason = (SessionSwitchReasonEx)(int)e.Reason;
      messenger.Send(new WindowsSessionSwitchedMessage(reason, Process.GetCurrentProcess().SessionId));

      appLifetime.StopApplication();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error changing session");
    }
  }
}