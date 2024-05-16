﻿using ControlR.Devices.Common.Native.Windows;
using ControlR.Shared.Dtos.SidecarDtos;
using ControlR.Shared.Enums;
using ControlR.Shared.Models;
using ControlR.Shared.Primitives;
using System.Diagnostics;

namespace ControlR.Devices.Common.Services;
internal class Win32InteropFake : IWin32Interop
{
    public bool CreateInteractiveSystemProcess(string commandLine, int targetSessionId, bool forceConsoleSession, string desktopName, bool hiddenWindow, out Process? startedProcess)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<WindowsSession> GetActiveSessions()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<WindowsSession> GetActiveSessionsCsWin32()
    {
        throw new NotImplementedException();
    }

    public bool GetCurrentThreadDesktop(out string desktopName)
    {
        throw new NotImplementedException();
    }

    public bool GetInputDesktop(out string desktopName)
    {
        throw new NotImplementedException();
    }

    public bool GetThreadDesktop(uint threadId, out string desktopName)
    {
        throw new NotImplementedException();
    }

    public string GetUsernameFromSessionId(uint sessionId)
    {
        throw new NotImplementedException();
    }

    public bool GlobalMemoryStatus(ref MemoryStatusEx lpBuffer)
    {
        throw new NotImplementedException();
    }

    public void InvokeCtrlAltDel()
    {
        throw new NotImplementedException();
    }

    public Result InvokeKeyEvent(string key, JsKeyType jsKeyType, bool isPressed)
    {
        throw new NotImplementedException();
    }

    public void InvokeMouseButtonEvent(int x, int y, int button, bool isPressed)
    {
        throw new NotImplementedException();
    }

    public void InvokeWheelScroll(int x, int y, int scrollY, int scrollX)
    {
        throw new NotImplementedException();
    }

    public void MovePointer(int x, int y, MovePointerType moveType)
    {
        throw new NotImplementedException();
    }

    public nint OpenInputDesktop()
    {
        throw new NotImplementedException();
    }

    public void ResetKeyboardState()
    {
        throw new NotImplementedException();
    }

    public bool SwitchToInputDesktop()
    {
        throw new NotImplementedException();
    }

    public void TypeText(string text)
    {
        throw new NotImplementedException();
    }
}