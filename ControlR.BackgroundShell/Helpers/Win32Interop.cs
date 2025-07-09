using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;

namespace ControlR.BackgroundShell.Helpers;

internal static class Win32Interop
{
  private const int DESKTOP_READOBJECTS = 0x0001;
  private const int DESKTOP_ENUMERATE = 0x0040;
  private const int SW_MINIMIZE = 6;
  private const int SW_RESTORE = 9;

  [DllImport("user32.dll", SetLastError = true)]
  private static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

  [DllImport("user32.dll", SetLastError = true)]
  private static extern bool CloseDesktop(IntPtr hDesktop);

  [DllImport("user32.dll", SetLastError = true)]
  private static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumDesktopWindowsProc lpfn, IntPtr lParam);

  private delegate bool EnumDesktopWindowsProc(IntPtr hWnd, IntPtr lParam);

  [DllImport("user32.dll", SetLastError = true)]
  private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

  [DllImport("user32.dll")]
  private static extern bool IsIconic(IntPtr hWnd);

  [DllImport("user32.dll")]
  private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

  [DllImport("user32.dll")]
  private static extern IntPtr GetForegroundWindow();

  [DllImport("user32.dll")]
  private static extern bool SetForegroundWindow(IntPtr hWnd);


  public static bool IsWindowFocused(IntPtr windowHandle)
  {
    return windowHandle == GetForegroundWindow();
  }

  internal static void CloseOtherProcesses()
  {
    var currentProcess = Process.GetCurrentProcess();
    var pids = new HashSet<int>();
    var hDesktop = OpenDesktop("ControlR_Desktop", 0, false, DESKTOP_ENUMERATE | DESKTOP_READOBJECTS);

    if (hDesktop == IntPtr.Zero)
      return;

    try
    {
      EnumDesktopWindows(hDesktop, (hWnd, lParam) =>
      {
        GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid != 0)
          pids.Add((int)pid);
        return true;
      }, IntPtr.Zero);
    }
    finally
    {
      CloseDesktop(hDesktop);
    }
    foreach (var pid in pids)
    {
      try
      {
        if (pid == currentProcess.Id)
          continue;

        var process = Process.GetProcessById(pid);
        if (string.Equals(process.ProcessName, "ControlR.Streamer", StringComparison.OrdinalIgnoreCase))
          continue;

        process.Kill();
      }
      catch
      {
        // Ignore processes that cannot be killed
      }
    }
  }

  internal static void FocusWindow(IntPtr mainWindowHandle)
  {
    ShowWindow(mainWindowHandle, SW_RESTORE);
    SetForegroundWindow(mainWindowHandle);
  }

  internal static IntPtr FindWindow(string windowTitle)
  {
    return FindWindow(null, windowTitle);
  }

  [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
  private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);
}