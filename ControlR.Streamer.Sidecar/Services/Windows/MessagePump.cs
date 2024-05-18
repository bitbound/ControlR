using Bitbound.SimpleMessenger;
using ControlR.Streamer.Sidecar.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace ControlR.Streamer.Sidecar.Services.Windows;

[SupportedOSPlatform("windows")]
internal class MessagePump(IMessenger _messenger, ILogger<MessagePump> _logger) : IHostedService
{
    private readonly CancellationTokenSource _stoppingCts = new();
    private Thread? _messageLoopThread;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _messageLoopThread = new Thread(() =>
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(5))
            {
                return;
            }

            _logger.LogInformation("Message pump starting.");

            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
            SystemEvents.SessionEnding += SystemEvents_SessionEnding;
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

            while (!_stoppingCts.Token.IsCancellationRequested)
            {
                try
                {
                    while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0))
                    {
                        PInvoke.TranslateMessage(in msg);
                        PInvoke.DispatchMessage(in msg);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in message loop.");
                }
            }

            _logger.LogInformation("Message pump ending.");
            SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
            SystemEvents.SessionEnding -= SystemEvents_SessionEnding;
            SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        });
        _messageLoopThread.SetApartmentState(ApartmentState.STA);
        _messageLoopThread.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stoppingCts.Cancel();
        return Task.CompletedTask;
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        _messenger.Send(new DisplaySettingsChangedMessage());
    }

    private void SystemEvents_SessionEnding(object? sender, SessionEndingEventArgs e)
    {
        _logger.LogInformation("Session ending.  Reason: {reason}", e.Reason);

        var reason = (SessionEndReasonsEx)e.Reason;
        _messenger.Send(new WindowsSessionEndingMessage(reason));
    }

    private void SystemEvents_SessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        _logger.LogInformation("Session changing.  Reason: {reason}", e.Reason);

        var reason = (SessionSwitchReasonEx)(int)e.Reason;
        _messenger.Send(new WindowsSessionSwitchedMessage(reason, Process.GetCurrentProcess().SessionId));
    }
}
