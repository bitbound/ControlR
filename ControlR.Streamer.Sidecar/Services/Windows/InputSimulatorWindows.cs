using ControlR.Devices.Common.Native.Windows;
using ControlR.Shared.Dtos.SidecarDtos;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace ControlR.Streamer.Sidecar.Services.Windows;

public interface IInputSimulator
{
    void InvokeKeyEvent(string key, bool isPressed);
    void InvokeMouseButtonEvent(int x, int y, int button, bool isPressed);
    void MovePointer(int x, int y, MovePointerType moveType);
    void ResetKeyboardState();
    void ScrollWheel(int x, int y, int scrollY);
    void TypeText(string text);
}

[SupportedOSPlatform("windows6.0.6000")]
internal class InputSimulatorWindows(ILogger<InputSimulatorWindows> _logger) : IInputSimulator
{
    public void InvokeKeyEvent(string key, bool isPressed)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogWarning("Key cannot be empty.");
            return;
        }

        Win32.SwitchToInputDesktop();
        var result = Win32.InvokeKeyEvent(key, isPressed);
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to invoke key event. Key: {Key}, IsPressed: {IsPressed}, Reason: {Reason}", key, isPressed, result.Reason);
        }
    }

    public void InvokeMouseButtonEvent(int x, int y, int button, bool isPressed)
    {
        Win32.SwitchToInputDesktop();
        Win32.InvokeMouseButtonEvent(x, y, button, isPressed);
    }

    public void MovePointer(int x, int y, MovePointerType moveType)
    {
        Win32.SwitchToInputDesktop();
        Win32.MovePointer(x, y, moveType);
    }

    public void ResetKeyboardState()
    {
        Win32.SwitchToInputDesktop();
        Win32.ResetKeyboardState();
    }

    public void ScrollWheel(int x, int y, int scrollY)
    {
        Win32.SwitchToInputDesktop();
        Win32.ScrollWheel(x, y, scrollY);
    }

    public void TypeText(string text)
    {
        Win32.SwitchToInputDesktop();
        Win32.TypeText(text);
    }
}
