using ControlR.Devices.Common.Native.Windows;
using ControlR.Shared.Dtos.SidecarDtos;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ControlR.Streamer.Sidecar.Services.Windows;

public interface IInputSimulator
{
    void InvokeMouseButtonEvent(int x, int y, int button, bool isPressed);
    void MovePointer(int x, int y, MovePointerType moveType);
}

[SupportedOSPlatform("windows6.0.6000")]
internal class InputSimulatorWindows : IInputSimulator
{
    public void InvokeMouseButtonEvent(int x, int y, int button, bool isPressed)
    {
        Win32.InvokeMouseButtonEvent(x, y, button, isPressed);
    }

    public void MovePointer(int x, int y, MovePointerType moveType)
    {
        Win32.MovePointer(x, y, moveType);
    }
}
