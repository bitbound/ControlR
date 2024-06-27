using Bitbound.SimpleMessenger;
using ControlR.Streamer.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;
using System.Diagnostics;

namespace ControlR.Streamer.Services;

internal class SystemEventHandler(
    IMessenger _messenger,
    IHostApplicationLifetime _appLifetime,
    ILogger<SystemEventHandler> _logger) : IHostedService
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
        _messenger.Send(new DisplaySettingsChangedMessage());
    }

    private async void SystemEvents_SessionEnding(object? sender, SessionEndingEventArgs e)
    {
        _logger.LogInformation("Session ending.  Reason: {reason}", e.Reason);

        var reason = (SessionEndReasonsEx)e.Reason;
        await _messenger.Send(new WindowsSessionEndingMessage(reason));

        _appLifetime.StopApplication();
    }

    private void SystemEvents_SessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        _logger.LogInformation("Session changing.  Reason: {reason}", e.Reason);

        var reason = (SessionSwitchReasonEx)(int)e.Reason;
        _messenger.Send(new WindowsSessionSwitchedMessage(reason, Process.GetCurrentProcess().SessionId));

        _appLifetime.StopApplication();
    }
}
