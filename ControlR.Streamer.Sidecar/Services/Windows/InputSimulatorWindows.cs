using ControlR.Devices.Common.Native.Windows;
using ControlR.Shared.Dtos.SidecarDtos;
using ControlR.Shared.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.Versioning;

namespace ControlR.Streamer.Sidecar.Services.Windows;

public interface IInputSimulator
{
    void InvokeKeyEvent(string key, JsKeyType jsKeyType, bool isPressed);
    void InvokeMouseButtonEvent(int x, int y, int button, bool isPressed);
    void MovePointer(int x, int y, MovePointerType moveType);
    void ResetKeyboardState();
    void ScrollWheel(int x, int y, int scrollY);
    void TypeText(string text);
}

[SupportedOSPlatform("windows6.0.6000")]
internal class InputSimulatorWindows: IInputSimulator
{
    private readonly ConcurrentQueue<Action> _actionQueue = new();
    private readonly AutoResetEvent _queueSignal = new(false);
    private readonly IWin32Interop _win32Interop;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<InputSimulatorWindows> _logger;
    private readonly Thread _processorThread;

    public InputSimulatorWindows(
        IWin32Interop win32Interop,
        IHostApplicationLifetime appLifetime,
        ILogger<InputSimulatorWindows> logger)
    {
        _win32Interop = win32Interop;
        _appLifetime = appLifetime;
        _logger = logger;
        _processorThread = new Thread(() =>
        {
            _logger.LogInformation("Input simulator processor thread started.");
            ProcessActions();
        });
        _processorThread.SetApartmentState(ApartmentState.STA);
        _processorThread.Start();
    }

    private void ProcessActions()
    {
        while (!_appLifetime.ApplicationStopping.IsCancellationRequested)
        {
            _queueSignal.WaitOne();
            while (_actionQueue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing input simulator action.");
                }
            }
        }
    }

    public void InvokeKeyEvent(string key, JsKeyType jsKeyType, bool isPressed)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogWarning("Key cannot be empty.");
            return;
        }

        _actionQueue.Enqueue(() => {
            _win32Interop.SwitchToInputDesktop();
            var result = _win32Interop.InvokeKeyEvent(key, jsKeyType, isPressed);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to invoke key event. Key: {Key}, IsPressed: {IsPressed}, Reason: {Reason}", key, isPressed, result.Reason);
            }
        });
        _queueSignal.Set();
    }

    public void InvokeMouseButtonEvent(int x, int y, int button, bool isPressed)
    {
        _actionQueue.Enqueue(() => {
            _win32Interop.SwitchToInputDesktop();
            _win32Interop.InvokeMouseButtonEvent(x, y, button, isPressed);
        });
        _queueSignal.Set();
    }

    public void MovePointer(int x, int y, MovePointerType moveType)
    {
        _actionQueue.Enqueue(() => {
            _win32Interop.SwitchToInputDesktop();
            _win32Interop.MovePointer(x, y, moveType);
        });
        _queueSignal.Set();
    }

    public void ResetKeyboardState()
    {
        _actionQueue.Enqueue(() => {
            _win32Interop.SwitchToInputDesktop();
            _win32Interop.ResetKeyboardState();
        });
        _queueSignal.Set();
    }

    public void ScrollWheel(int x, int y, int scrollY)
    {
        _actionQueue.Enqueue(() => {
            _win32Interop.SwitchToInputDesktop();
            _win32Interop.InvokeWheelScroll(x, y, scrollY);
        });
        _queueSignal.Set();
    }

    public void TypeText(string text)
    {
        _actionQueue.Enqueue(() => {
            _win32Interop.SwitchToInputDesktop();
            _win32Interop.TypeText(text);
        });
        _queueSignal.Set();
    }
}
