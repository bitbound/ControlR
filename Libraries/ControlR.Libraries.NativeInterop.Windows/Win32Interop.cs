// ReSharper disable IdentifierTypo
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.StationsAndDesktops;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using WTS_CONNECTSTATE_CLASS = Windows.Win32.System.RemoteDesktop.WTS_CONNECTSTATE_CLASS;
using WTS_INFO_CLASS = Windows.Win32.System.RemoteDesktop.WTS_INFO_CLASS;
using WTS_SESSION_INFOW = Windows.Win32.System.RemoteDesktop.WTS_SESSION_INFOW;

namespace ControlR.Libraries.NativeInterop.Windows;

public interface IWin32Interop
{
  bool CreateInteractiveSystemProcess(
    string commandLine,
    int targetSessionId,
    bool hiddenWindow,
    [NotNullWhen(true)] out Process? startedProcess);

  bool EnumWindows(Func<nint, bool> windowFunc);
  List<DesktopSession> GetActiveSessions();
  List<DesktopSession> GetActiveSessionsCsWin32();
  string? GetClipboardText();
  PointerCursor GetCurrentCursor();
  bool GetCurrentThreadDesktopName(out string currentDesktop);
  List<string> GetDesktopNames(string windowStation = "WinSta0");
  bool GetInputDesktopName([NotNullWhen(true)] out string? inputDesktop);
  nint GetParentWindow(nint windowHandle);
  bool GetThreadDesktopName(uint threadId, [NotNullWhen(true)] out string? desktopName);
  string GetUsernameFromSessionId(uint sessionId);
  List<WindowInfo> GetVisibleWindows();
  bool GetWindowRect(nint windowHandle, out Rectangle windowRect);
  bool GlobalMemoryStatus(ref MemoryStatusEx lpBuffer);
  void InvokeCtrlAltDel();
  void InvokeKeyEvent(string key, bool isPressed);
  void InvokeMouseButtonEvent(int x, int y, int button, bool isPressed);
  void InvokeWheelScroll(int x, int y, int scrollY, int scrollX);
  void MovePointer(int x, int y, MovePointerType moveType);
  nint OpenInputDesktop();
  void ResetKeyboardState();
  void SetBlockInput(bool isBlocked);
  void SetClipboardText(string? text);
  nint SetParentWindow(nint windowHandle, nint parentWindowHandle);
  bool SetWindowPos(nint mainWindowHandle, nint insertAfter, int x, int y, int width, int height);

  bool StartProcessInBackgroundSession(
    string commandLine,
    bool hiddenWindow,
    [NotNullWhen(true)] out Process? startedProcess);

  bool SwitchToInputDesktop();
  void TypeText(string text);
}

[SupportedOSPlatform("windows6.0.6000")]
public unsafe partial class Win32Interop(ILogger<Win32Interop> logger) : IWin32Interop
{
  private const uint CF_UNICODETEXT = 13u;
  private const uint GenericAllRights = 0x10000000u;
  private const uint MaximumAllowedRights = 0x2000000u;
  private const uint Xbutton1 = 0x0001u;
  private const uint Xbutton2 = 0x0002u;

  private static readonly string[] _invalidWindowClassNames =
  [
    "Progman", // Desktop
    "WorkerW", // Desktop worker
    "Shell_TrayWnd", // Taskbar
    "DV2ControlHost", // Windows widgets
    "Windows.UI.Core.CoreWindow", // UWP system windows
    "ApplicationFrameWindow", // Some UWP containers
    "ForegroundStaging", // System staging window
    "MSCTFIME UI" // Input method editor
  ];

  private readonly ILogger<Win32Interop> _logger = logger;

  private FrozenDictionary<HCURSOR, PointerCursor>? _cursorMap;
  private HDESK _lastInputDesktop;

  public bool CreateInteractiveSystemProcess(
    string commandLine,
    int targetSessionId,
    bool hiddenWindow,
    [NotNullWhen(true)] out Process? startedProcess)
  {
    startedProcess = null;
    try
    {
      uint winLogonPid = 0;

      var sessionId = ResolveWindowsSession(targetSessionId);

      var winLogonProcs = Process.GetProcessesByName("winlogon");
      foreach (var p in winLogonProcs)
      {
        if ((uint)p.SessionId == sessionId)
        {
          winLogonPid = (uint)p.Id;
        }
      }

      // Obtain a handle to the winlogon process;
      var winLogonProcessHandle = PInvoke.OpenProcess(
        (PROCESS_ACCESS_RIGHTS)MaximumAllowedRights,
        true,
        winLogonPid);

      // Obtain a handle to the access token of the winlogon process.
      using var winLogonSafeProcHandle = new SafeProcessHandle(winLogonProcessHandle, true);
      if (!PInvoke.OpenProcessToken(winLogonSafeProcHandle, TOKEN_ACCESS_MASK.TOKEN_DUPLICATE, out var winLogonToken))
      {
        _logger.LogWarning("Failed to open winlogon process.");
        PInvoke.CloseHandle(winLogonProcessHandle);
        return false;
      }

      // Security attribute structure used in DuplicateTokenEx and CreateProcessAsUser.
      var securityAttributes = new SECURITY_ATTRIBUTES
      {
        nLength = (uint)sizeof(SECURITY_ATTRIBUTES)
      };

      // Copy the access token of the winlogon process; the newly created token will be a primary token.
      if (!PInvoke.DuplicateTokenEx(
            winLogonToken,
            (TOKEN_ACCESS_MASK)MaximumAllowedRights,
            null,
            SECURITY_IMPERSONATION_LEVEL.SecurityIdentification,
            TOKEN_TYPE.TokenPrimary,
            out var duplicatedToken))
      {
        PInvoke.CloseHandle(winLogonProcessHandle);
        winLogonToken.Close();
        return false;
      }

      // Target the interactive windows station and desktop.
      var startupInfo = new STARTUPINFOW
      {
        cb = (uint)sizeof(STARTUPINFOW)
      };

      var desktopName = ResolveDesktopName(sessionId);
      var desktopPtr = Marshal.StringToHGlobalAuto($"winsta0\\{desktopName}\0");
      startupInfo.lpDesktop = new PWSTR((char*)desktopPtr.ToPointer());

      // Flags that specify the priority and creation method of the process.
      var dwCreationFlags = PROCESS_CREATION_FLAGS.NORMAL_PRIORITY_CLASS |
                            PROCESS_CREATION_FLAGS.CREATE_UNICODE_ENVIRONMENT;

      if (hiddenWindow)
      {
        dwCreationFlags |= PROCESS_CREATION_FLAGS.CREATE_NO_WINDOW | PROCESS_CREATION_FLAGS.DETACHED_PROCESS;
        startupInfo.dwFlags = STARTUPINFOW_FLAGS.STARTF_USESHOWWINDOW;
        startupInfo.wShowWindow = 0;
      }
      else
      {
        dwCreationFlags |= PROCESS_CREATION_FLAGS.CREATE_NEW_CONSOLE;
      }

      var cmdLineSpan = $"{commandLine}\0".ToCharArray().AsSpan();
      // Create a new process in the current user's logon session.
      var createResult = PInvoke.CreateProcessAsUser(
        duplicatedToken,
        null,
        ref cmdLineSpan,
        securityAttributes,
        securityAttributes,
        false,
        dwCreationFlags,
        null,
        null,
        in startupInfo,
        out var procInfo);

      // Invalidate the handles.
      PInvoke.CloseHandle(winLogonProcessHandle);
      Marshal.FreeHGlobal(desktopPtr);
      winLogonToken.Close();
      duplicatedToken.Close();

      if (!createResult)
      {
        var lastWin32 = Marshal.GetLastWin32Error();
        _logger.LogError("CreateProcessAsUser failed.  Last Win32 error: {LastWin32Error}", lastWin32);
        return false;
      }

      startedProcess = Process.GetProcessById((int)procInfo.dwProcessId);
      return true;
    }
    catch (Exception ex)
    {
      var lastWin32 = Marshal.GetLastWin32Error();
      _logger.LogError(ex, "Error while starting interactive system process.  Last Win32 error: {LastWin32Error}",
        lastWin32);
      return false;
    }
  }

  public bool EnumWindows(Func<nint, bool> windowFunc)
  {
    return PInvoke.EnumWindows((hwnd, _) => windowFunc.Invoke((nint)hwnd.Value),
      nint.Zero);
  }

  public List<DesktopSession> GetActiveSessions()
  {
    var sessions = new List<DesktopSession>();
    var consoleSessionId = PInvoke.WTSGetActiveConsoleSessionId();
    sessions.Add(new DesktopSession
    {
      SystemSessionId = (int)consoleSessionId,
      Type = DesktopSessionType.Console,
      Name = "Console",
      Username = GetUsernameFromSessionId(consoleSessionId)
    });

    var ppSessionInfo = nint.Zero;
    var count = 0;
    var enumSessionResult =
      WtsApi32.WTSEnumerateSessions(WtsApi32.WtsCurrentServerHandle, 0, 1, ref ppSessionInfo, ref count);
    var dataSize = Marshal.SizeOf<WtsApi32.WtsSessionInfo>();
    var current = ppSessionInfo;

    if (enumSessionResult == 0)
    {
      return sessions;
    }

    for (var i = 0; i < count; i++)
    {
      try
      {
        var sessionInfo = Marshal.PtrToStructure<WtsApi32.WtsSessionInfo>(current);
        current += dataSize;
        if (sessionInfo.State == WtsApi32.WtsConnectstateClass.WtsActive && sessionInfo.SessionID != consoleSessionId)
        {
          sessions.Add(new DesktopSession
          {
            SystemSessionId = (int)sessionInfo.SessionID,
            Name = sessionInfo.pWinStationName,
            Type = DesktopSessionType.Rdp,
            Username = GetUsernameFromSessionId(sessionInfo.SessionID)
          });
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to marshal active session.");
      }
    }

    WtsApi32.WTSFreeMemory(ppSessionInfo);

    return sessions;
  }

  public List<DesktopSession> GetActiveSessionsCsWin32()
  {
    var sessions = new List<DesktopSession>();

    var consoleSessionId = PInvoke.WTSGetActiveConsoleSessionId();
    sessions.Add(new DesktopSession
    {
      SystemSessionId = (int)consoleSessionId,
      Type = DesktopSessionType.Console,
      Name = "Console",
      Username = GetUsernameFromSessionId(consoleSessionId)
    });

    var enumSessionResult = PInvoke.WTSEnumerateSessions(
      HANDLE.WTS_CURRENT_SERVER_HANDLE,
      0,
      1,
      out var ppSessionInfos,
      out var count);

    if (!enumSessionResult)
    {
      return [.. sessions];
    }

    var dataSize = sizeof(WTS_SESSION_INFOW);

    for (var i = 0; i < count; i++)
    {
      if (ppSessionInfos->State == WTS_CONNECTSTATE_CLASS.WTSActive && ppSessionInfos->SessionId != consoleSessionId)
      {
        sessions.Add(new DesktopSession
        {
          SystemSessionId = (int)ppSessionInfos->SessionId,
          Name = ppSessionInfos->pWinStationName.ToString(),
          Type = DesktopSessionType.Rdp,
          Username = GetUsernameFromSessionId(ppSessionInfos->SessionId)
        });
      }

      ppSessionInfos += dataSize;
    }

    PInvoke.WTSFreeMemory(ppSessionInfos);

    return [.. sessions];
  }

  public string? GetClipboardText()
  {
    try
    {
      if (!PInvoke.OpenClipboard(HWND.Null))
      {
        _logger.LogError("Failed to open clipboard.");
        LogWin32Error();
        return null;
      }

      var data = PInvoke.GetClipboardData(1);
      return Marshal.PtrToStringAnsi(data);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting clipboard text.");
      return null;
    }
    finally
    {
      _ = PInvoke.CloseClipboard();
    }
  }

  public PointerCursor GetCurrentCursor()
  {
    try
    {
      var cursorInfo = new CURSORINFO
      {
        cbSize = (uint)sizeof(CURSORINFO)
      };

      if (!PInvoke.GetCursorInfo(ref cursorInfo) || cursorInfo.hCursor == default)
      {
        _logger.LogDebug("Failed to get cursor info.  Last p/invoke error: {LastError}",
          Marshal.GetLastPInvokeErrorMessage());
        return PointerCursor.Unknown;
      }

      if (GetCursorMap().TryGetValue(cursorInfo.hCursor, out var cursor))
      {
        return cursor;
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting current cursor.");
    }

    return PointerCursor.Unknown;
  }

  public bool GetCurrentThreadDesktopName(out string desktopName)
  {
    desktopName = string.Empty;
    var threadId = PInvoke.GetCurrentThreadId();
    var desktop = PInvoke.GetThreadDesktop(threadId);
    if (desktop == nint.Zero)
    {
      return false;
    }

    return GetDesktopName(desktop, out desktopName);
  }

  public List<string> GetDesktopNames(string windowStation = "WinSta0")
  {
    using var winsta = PInvoke.OpenWindowStation(windowStation, true, GenericAllRights);
    var desktopNames = new List<string>();
    _ = PInvoke.EnumDesktops(
      winsta,
      (desktopName, _) =>
      {
        desktopNames.Add(desktopName.ToString());
        return true;
      },
      default);

    return desktopNames;
  }

  public bool GetInputDesktopName([NotNullWhen(true)] out string? desktopName)
  {
    var inputDesktop = PInvoke.OpenInputDesktop(0, true, (DESKTOP_ACCESS_FLAGS)GenericAllRights);
    try
    {
      return GetDesktopName(inputDesktop, out desktopName);
    }
    finally
    {
      PInvoke.CloseDesktop(inputDesktop);
    }
  }

  public nint GetParentWindow(nint windowHandle)
  {
    return (nint)PInvoke.GetParent(new HWND(windowHandle)).Value;
  }

  public bool GetThreadDesktopName(uint threadId, out string desktopName)
  {
    var hdesk = PInvoke.GetThreadDesktop(threadId);
    if (hdesk.IsNull)
    {
      desktopName = string.Empty;
      return false;
    }

    return GetDesktopName(hdesk, out desktopName);
  }

  public string GetUsernameFromSessionId(uint sessionId)
  {
    var result = PInvoke.WTSQuerySessionInformation(HANDLE.Null, sessionId, WTS_INFO_CLASS.WTSUserName,
      out var username, out var bytesReturned);

    if (result && bytesReturned > 1)
    {
      return username.ToString();
    }

    return string.Empty;
  }

  public List<WindowInfo> GetVisibleWindows()
  {
    var handles = new List<nint>();
    var windows = new List<WindowInfo>();

    _ = PInvoke.EnumWindows(
      (hwnd, _) =>
      {
        if (hwnd == HWND.Null)
        {
          return true;
        }

        handles.Add(hwnd);
        return true;
      },
      nint.Zero);

    foreach (var item in handles.Index())
    {
      var hwnd = new HWND(item.Item);

      if (!PInvoke.GetWindowRect(hwnd, out var windowRect))
      {
        continue;
      }

      if (!PInvoke.IsWindowVisible(hwnd))
      {
        continue;
      }

      if (windowRect.Size.IsEmpty)
      {
        continue;
      }

      var className = GetWindowClassName(hwnd);

      if (_invalidWindowClassNames.Contains(className, StringComparer.OrdinalIgnoreCase))
      {
        continue;
      }

      var pwi = new WINDOWINFO
      {
        cbSize = (uint)Marshal.SizeOf<WINDOWINFO>()
      };
      if (!PInvoke.GetWindowInfo(hwnd, ref pwi))
      {
        _logger.LogDebug("Failed to get window info for handle {WindowHandle}. Last Win32 Error: {LastWin32Error}",
          hwnd,
          Marshal.GetLastWin32Error());
      }

      var windowTextLength = PInvoke.GetWindowTextLength(hwnd);
      var windowTextSpan = new char[windowTextLength + 1].AsSpan();
      _ = PInvoke.GetWindowText(hwnd, windowTextSpan);
      var windowText = new string(windowTextSpan).Trim().TrimEnd('\0');

      var processId = PInvoke.GetWindowThreadProcessId(hwnd);
      var processName = $"Process-{processId}";
      try
      {
        var windowProcess = Process.GetProcessById((int)processId);
        processName = windowProcess.ProcessName;
      }
      catch
      {
        // Ignore.
      }

      windows.Add(new WindowInfo(
        hwnd,
        (int)processId,
        processName,
        windowText,
        windowRect.X,
        windowRect.Y,
        windowRect.Width,
        windowRect.Height,
        item.Index));
    }

    return windows;
  }

  public bool GetWindowRect(nint windowHandle, out Rectangle windowRect)
  {
    windowRect = Rectangle.Empty;

    if (!PInvoke.GetWindowRect(new HWND(windowHandle), out var rect))
    {
      return false;
    }

    windowRect = new Rectangle(
      rect.X,
      rect.Y,
      rect.Width,
      rect.Height);

    return true;
  }

  public bool GlobalMemoryStatus(ref MemoryStatusEx lpBuffer)
  {
    return GlobalMemoryStatusEx(ref lpBuffer);
  }

  [SupportedOSPlatform("windows6.1")]
  public void InvokeCtrlAltDel()
  {
    var isService = Process.GetCurrentProcess().SessionId == 0;
    PInvoke.SendSAS(!isService);
  }

  public void InvokeKeyEvent(string key, bool isPressed)
  {
    if (!ConvertBrowserKeyArgToVirtualKey(key, out var convertResult))
    {
      _logger.LogWarning("Failed to convert key '{Key}' to virtual key.", key);
      return;
    }

    var kbdInput = CreateKeyboardInput(convertResult.Value, isPressed);

    var result = PInvoke.SendInput([kbdInput], sizeof(INPUT));

    if (result == 0)
    {
      _logger.LogWarning("Failed to send key input. Key: {Key}, IsPressed: {IsPressed}.", key, isPressed);
    }
  }

  public void InvokeMouseButtonEvent(int x, int y, int button, bool isPressed)
  {
    MovePointer(x, y, MovePointerType.Absolute);

    var extraInfo = PInvoke.GetMessageExtraInfo();
    MOUSE_EVENT_FLAGS mouseEventFlags = 0;
    uint mouseData = 0;

    switch (button)
    {
      case 0:
        if (isPressed)
        {
          mouseEventFlags |= MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN;
        }
        else
        {
          mouseEventFlags |= MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP;
        }

        break;
      case 1:
        if (isPressed)
        {
          mouseEventFlags |= MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEDOWN;
        }
        else
        {
          mouseEventFlags |= MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEUP;
        }

        break;
      case 2:
        if (isPressed)
        {
          mouseEventFlags |= MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN;
        }
        else
        {
          mouseEventFlags |= MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP;
        }

        break;
      case 3:
        mouseData = Xbutton1;
        if (isPressed)
        {
          mouseEventFlags |= MOUSE_EVENT_FLAGS.MOUSEEVENTF_XDOWN;
        }
        else
        {
          mouseEventFlags |= MOUSE_EVENT_FLAGS.MOUSEEVENTF_XUP;
        }

        break;
      case 4:
        mouseData = Xbutton2;
        if (isPressed)
        {
          mouseEventFlags |= MOUSE_EVENT_FLAGS.MOUSEEVENTF_XDOWN;
        }
        else
        {
          mouseEventFlags |= MOUSE_EVENT_FLAGS.MOUSEEVENTF_XUP;
        }

        break;
      default:
        return;
    }

    var mouseInput = new MOUSEINPUT
    {
      mouseData = mouseData,
      dwFlags = mouseEventFlags,
      dwExtraInfo = new nuint(extraInfo.Value.ToPointer())
    };

    var input = new INPUT
    {
      type = INPUT_TYPE.INPUT_MOUSE,
      Anonymous = { mi = mouseInput }
    };

    var result = PInvoke.SendInput([input], sizeof(INPUT));
    if (result == 0)
    {
      _logger.LogWarning("Failed to send mouse input: {MouseInput}.", mouseInput);
    }
  }

  public void InvokeWheelScroll(int x, int y, int scrollY, int scrollX)
  {
    if (Math.Abs(scrollY) > 0)
    {
      InvokeWheelScroll(x, y, scrollY, true);
    }

    if (Math.Abs(scrollX) > 0)
    {
      InvokeWheelScroll(x, y, scrollX, false);
    }
  }

  public void MovePointer(int x, int y, MovePointerType moveType)
  {
    var input = GetPointerMoveInput(x, y, moveType);

    var result = PInvoke.SendInput([input], sizeof(INPUT));
    if (result == 0)
    {
      _logger.LogWarning("Failed to send pointer move input: {@MouseInput}.", input);
    }
  }

  public nint OpenInputDesktop()
  {
    return PInvoke.OpenInputDesktop(0, true, (DESKTOP_ACCESS_FLAGS)0x10000000u);
  }

  public void ResetKeyboardState()
  {
    var extraInfo = PInvoke.GetMessageExtraInfo();
    var inputs = new List<INPUT>();

    foreach (var key in Enum.GetValues<VIRTUAL_KEY>())
    {
      try
      {
        var state = PInvoke.GetAsyncKeyState((int)key);

        switch (key)
        {
          // Skip mouse buttons and toggleable keys.
          case VIRTUAL_KEY.VK_LBUTTON:
          case VIRTUAL_KEY.VK_RBUTTON:
          case VIRTUAL_KEY.VK_MBUTTON:
          case VIRTUAL_KEY.VK_NUMLOCK:
          case VIRTUAL_KEY.VK_CAPITAL:
          case VIRTUAL_KEY.VK_SCROLL:
            continue;
        }

        if (state == 0)
        {
          continue;
        }

        var kbdInput = new KEYBDINPUT
        {
          wVk = key,
          dwExtraInfo = new nuint(extraInfo.Value.ToPointer()),
          dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP
        };

        var input = new INPUT
        {
          type = INPUT_TYPE.INPUT_KEYBOARD,
          Anonymous = { ki = kbdInput }
        };

        inputs.Add(input);
      }
      catch
      {
        // Ignore.
      }
    }

    foreach (var input in inputs)
    {
      var result = PInvoke.SendInput([input], sizeof(INPUT));
      if (result != 1)
      {
        _logger.LogWarning("Failed to reset key state.");
      }

      Thread.Sleep(1);
    }
  }

  public void SetBlockInput(bool isBlocked)
  {
    if (!PInvoke.BlockInput(isBlocked))
    {
      _logger.LogError("Failed to set input block state to {IsBlocked}.", isBlocked);
      LogWin32Error();
    }
  }

  public void SetClipboardText(string? text)
  {
    if (string.IsNullOrWhiteSpace(text))
    {
      return;
    }

    if (!PInvoke.OpenClipboard(HWND.Null))
    {
      _logger.LogError("Failed to open clipboard.");
      LogWin32Error();
      return;
    }

    try
    {
      if (!PInvoke.EmptyClipboard())
      {
        _logger.LogError("Empty clipboard failed when trying to set text.");
        LogWin32Error();
        return;
      }

      var pointer = Marshal.StringToHGlobalUni(text);
      var handle = new HANDLE(pointer);
      var result = PInvoke.SetClipboardData(CF_UNICODETEXT, handle);
      if (result == default)
      {
        LogWin32Error();
        _logger.LogError("Failed to set clipboard text.  Last Win32 Error: {Win32Error}",
          Marshal.GetLastPInvokeError());
        Marshal.FreeHGlobal(pointer);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while setting clipboard text.");
    }
    finally
    {
      if (!PInvoke.CloseClipboard())
      {
        _logger.LogError("Failed to close clipboard.");
        LogWin32Error();
      }
    }
  }

  public nint SetParentWindow(nint windowHandle, nint parentWindowHandle)
  {
    return (nint)PInvoke.SetParent(new HWND(windowHandle), new HWND(parentWindowHandle)).Value;
  }

  public bool SetWindowPos(nint mainWindowHandle, nint insertAfter, int x, int y, int width, int height)
  {
    return PInvoke.SetWindowPos(
      new HWND(mainWindowHandle),
      new HWND(insertAfter),
      x,
      y,
      width,
      height,
      SET_WINDOW_POS_FLAGS.SWP_ASYNCWINDOWPOS | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW |
      SET_WINDOW_POS_FLAGS.SWP_DRAWFRAME | SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED);
  }

  public bool StartProcessInBackgroundSession(
    string commandLine,
    bool hiddenWindow,
    [NotNullWhen(true)] out Process? startedProcess)
  {
    startedProcess = null;

    // By default, the following window stations exist in session 0:
    // - WinSta0 (default)
    // - Service-0x0-3e7$
    // - Service-0x0-3e4$
    // - Service-0x0-3e5$
    // - msswindowstation

    if (!TryCloneSystemPrimaryToken(out var primaryToken))
    {
      return false;
    }

    using var primaryTokenCallback = new CallbackDisposable(primaryToken.Close);
    uint tokenSize = sizeof(uint);
    var tokenValue = stackalloc uint[] { 1 };

    if (!PInvoke.SetTokenInformation(primaryToken, TOKEN_INFORMATION_CLASS.TokenUIAccess, tokenValue, tokenSize))
    {
      _logger.LogWarning("Failed to set token UI access on duplicated token.");
      return false;
    }

    using var openWinstaResult = PInvoke.OpenWindowStation(
      "WinSta0",
      false,
      (uint)ACCESS_MASK.WINSTA_ALL_ACCESS);

    if (openWinstaResult.IsInvalid)
    {
      LogWin32Error();
      _logger.LogError("Failed to open window station.");
      return false;
    }

    var setProcessWinstaResult = PInvoke.SetProcessWindowStation(openWinstaResult);

    if (!setProcessWinstaResult)
    {
      LogWin32Error();
      _logger.LogError("Failed to set process window station.");
      return false;
    }

    var desktopName = "ControlR_Desktop";
    var sa = new SECURITY_ATTRIBUTES();
    sa.nLength = (uint)Marshal.SizeOf(sa);

    _logger.LogInformation("Getting/Creating background desktop: {DesktopName}", desktopName);
    using var backgroundDesktop = PInvoke.CreateDesktop(
      desktopName,
      0,
      (uint)ACCESS_MASK.DESKTOP_ALL,
      sa);

    if (backgroundDesktop.IsInvalid)
    {
      LogWin32Error();
      _logger.LogError("Failed to create desktop.");
      return false;
    }

    if (!PInvoke.SwitchDesktop(backgroundDesktop))
    {
      LogWin32Error();
      _logger.LogError("Failed to switch desktop.");
      return false;
    }

    if (!PInvoke.SetThreadDesktop(backgroundDesktop))
    {
      LogWin32Error();
      _logger.LogError("Failed to set thread desktop.");
      return false;
    }

    var desktopPtr = Marshal.StringToHGlobalAuto($"WinSta0\\{desktopName}\0");
    using var desktopPtrCb = new CallbackDisposable(() => Marshal.FreeHGlobal(desktopPtr));

    var startupInfo = new STARTUPINFOW
    {
      lpDesktop = new PWSTR((char*)desktopPtr.ToPointer())
    };

    // Flags that specify the priority and creation method of the process.
    var dwCreationFlags = PROCESS_CREATION_FLAGS.HIGH_PRIORITY_CLASS |
                          PROCESS_CREATION_FLAGS.CREATE_UNICODE_ENVIRONMENT;

    if (hiddenWindow)
    {
      dwCreationFlags |= PROCESS_CREATION_FLAGS.CREATE_NO_WINDOW | PROCESS_CREATION_FLAGS.DETACHED_PROCESS;
      startupInfo.dwFlags = STARTUPINFOW_FLAGS.STARTF_USESHOWWINDOW;
      startupInfo.wShowWindow = 0;
    }
    else
    {
      dwCreationFlags |= PROCESS_CREATION_FLAGS.CREATE_NEW_CONSOLE;
    }

    startupInfo.cb = (uint)Marshal.SizeOf(startupInfo);

    var commandLineArray = $"{commandLine}\0".ToCharArray();
    var commandLineSpan = commandLineArray.AsSpan();
    var createProcessResult = PInvoke.CreateProcessAsUser(
      primaryToken,
      null,
      ref commandLineSpan,
      sa,
      sa,
      false,
      dwCreationFlags,
      null,
      null,
      in startupInfo,
      out var procInfo);

    if (!createProcessResult)
    {
      LogWin32Error();
      _logger.LogError("Failed to create process.");
      return false;
    }

    startedProcess = Process.GetProcessById((int)procInfo.dwProcessId);
    return true;
  }

  public bool SwitchToInputDesktop()
  {
    try
    {
      if (!_lastInputDesktop.IsNull)
      {
        PInvoke.CloseDesktop(_lastInputDesktop);
      }

      var inputDesktop = GetInputDesktop();
      if (inputDesktop.IsNull)
      {
        return false;
      }

      var result = PInvoke.SetThreadDesktop(inputDesktop);
      _lastInputDesktop = inputDesktop;
      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while switching to input desktop.");
      LogWin32Error();
      return false;
    }
  }

  public void TypeText(string text)
  {
    var inputs = new List<INPUT>();

    foreach (var character in text)
    {
      ushort scanCode = character;

      var flags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE;

      var down = new INPUT
      {
        type = INPUT_TYPE.INPUT_KEYBOARD,
        Anonymous =
        {
          ki = new KEYBDINPUT
          {
            wVk = 0,
            wScan = scanCode,
            dwFlags = flags,
            dwExtraInfo = new nuint(PInvoke.GetMessageExtraInfo().Value.ToPointer()),
            time = 0
          }
        }
      };

      var up = new INPUT
      {
        type = INPUT_TYPE.INPUT_KEYBOARD,
        Anonymous =
        {
          ki = new KEYBDINPUT
          {
            wVk = 0,
            wScan = scanCode,
            dwFlags = flags | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
            dwExtraInfo = new nuint(PInvoke.GetMessageExtraInfo().Value.ToPointer()),
            time = 0
          }
        }
      };

      inputs.Add(down);
      inputs.Add(up);
    }

    foreach (var input in inputs)
    {
      var result = PInvoke.SendInput([input], sizeof(INPUT));
      if (result != 1)
      {
        _logger.LogWarning("Failed to type character in text.");
      }

      Thread.Sleep(1);
    }
  }

  private static INPUT CreateKeyboardInput(VIRTUAL_KEY key, bool isPressed)
  {
    var extraInfo = PInvoke.GetMessageExtraInfo();
    KEYBD_EVENT_FLAGS kbdFlags = 0;

    if (IsExtendedKey(key))
    {
      kbdFlags |= KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY;
    }

    if (!isPressed)
    {
      kbdFlags |= KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
    }

    var kbdInput = new KEYBDINPUT
    {
      wVk = key,
      wScan = (ushort)PInvoke.MapVirtualKeyEx((uint)key, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC_EX, GetKeyboardLayout()),
      dwExtraInfo = new nuint(extraInfo.Value.ToPointer()),
      dwFlags = kbdFlags,
      time = 0
    };

    var input = new INPUT
    {
      type = INPUT_TYPE.INPUT_KEYBOARD,
      Anonymous = { ki = kbdInput }
    };
    return input;
  }

  private static bool GetDesktopName(HDESK handle, out string desktopName)
  {
    var outValue = Marshal.AllocHGlobal(256);
    var outLength = Marshal.AllocHGlobal(256);
    var deskHandle = new HANDLE(handle.Value);

    if (!PInvoke.GetUserObjectInformation(
          deskHandle,
          USER_OBJECT_INFORMATION_INDEX.UOI_NAME,
          outValue.ToPointer(),
          256,
          (uint*)outLength.ToPointer()))
    {
      desktopName = string.Empty;
      return false;
    }

    desktopName = Marshal.PtrToStringAuto(outValue)?.Trim() ?? string.Empty;
    return !string.IsNullOrWhiteSpace(desktopName);
  }

  private static HDESK GetInputDesktop()
  {
    return PInvoke.OpenInputDesktop(0, true, (DESKTOP_ACCESS_FLAGS)GenericAllRights);
  }

  private static HKL GetKeyboardLayout()
  {
    return PInvoke.GetKeyboardLayout((uint)Environment.CurrentManagedThreadId);
  }

  private static Point GetNormalizedPoint(int x, int y)
  {
    var left = (double)PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN);
    var top = (double)PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN);
    var width = (double)PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
    var height = (double)PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);
    var right = left + width;
    var bottom = top + height;

    var normalizedX = (int)((x - left) / (right - left) * 65535);
    var normalizedY = (int)((y - top) / (bottom - top) * 65535);

    return new Point(normalizedX, normalizedY);
  }

  private static INPUT GetPointerMoveInput(int x, int y, MovePointerType moveType)
  {
    var extraInfo = PInvoke.GetMessageExtraInfo();
    var mouseEventFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE | MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK;

    if (moveType == MovePointerType.Absolute)
    {
      mouseEventFlags |= MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE;
    }

    var normalizedPoint = GetNormalizedPoint(x, y);

    var mouseInput = new MOUSEINPUT
    {
      dx = normalizedPoint.X,
      dy = normalizedPoint.Y,
      dwFlags = mouseEventFlags,
      mouseData = 0,
      dwExtraInfo = new nuint(extraInfo.Value.ToPointer())
    };

    return new INPUT
    {
      type = INPUT_TYPE.INPUT_MOUSE,
      Anonymous = { mi = mouseInput }
    };
  }

  private static string GetWindowClassName(HWND hwnd)
  {
    var buffer = stackalloc char[256];
    var length = PInvoke.GetClassName(hwnd, buffer, 256);
    return length > 0
      ? new string(buffer, 0, length)
      : string.Empty;
  }

  [return: MarshalAs(UnmanagedType.Bool)]
  [LibraryImport("kernel32.dll", SetLastError = true)]
  private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

  private static bool IsExtendedKey(VIRTUAL_KEY vk)
  {
    return vk switch
    {
      VIRTUAL_KEY.VK_SHIFT or
        VIRTUAL_KEY.VK_CONTROL or
        VIRTUAL_KEY.VK_MENU or
        VIRTUAL_KEY.VK_RCONTROL or
        VIRTUAL_KEY.VK_RMENU or
        VIRTUAL_KEY.VK_INSERT or
        VIRTUAL_KEY.VK_DELETE or
        VIRTUAL_KEY.VK_HOME or
        VIRTUAL_KEY.VK_END or
        VIRTUAL_KEY.VK_PRIOR or
        VIRTUAL_KEY.VK_NEXT or
        VIRTUAL_KEY.VK_LEFT or
        VIRTUAL_KEY.VK_UP or
        VIRTUAL_KEY.VK_RIGHT or
        VIRTUAL_KEY.VK_DOWN or
        VIRTUAL_KEY.VK_NUMLOCK or
        VIRTUAL_KEY.VK_CANCEL or
        VIRTUAL_KEY.VK_DIVIDE or
        VIRTUAL_KEY.VK_SNAPSHOT or
        VIRTUAL_KEY.VK_RETURN => true,
      _ => false
    };
  }

  private static string ResolveDesktopName(uint targetSessionId)
  {
    var isLogonScreenVisible = Process
      .GetProcessesByName("LogonUI")
      .Any(x => x.SessionId == targetSessionId);

    var isSecureDesktopVisible = Process
      .GetProcessesByName("consent")
      .Any(x => x.SessionId == targetSessionId);

    if (isLogonScreenVisible || isSecureDesktopVisible)
    {
      return "Winlogon";
    }

    return "Default";
  }

  private bool ConvertBrowserKeyArgToVirtualKey(string key, [NotNullWhen(true)] out VIRTUAL_KEY? result)
  {
    result = key switch
    {
      " " => VIRTUAL_KEY.VK_SPACE,
      "Down" or "ArrowDown" => VIRTUAL_KEY.VK_DOWN,
      "Up" or "ArrowUp" => VIRTUAL_KEY.VK_UP,
      "Left" or "ArrowLeft" => VIRTUAL_KEY.VK_LEFT,
      "Right" or "ArrowRight" => VIRTUAL_KEY.VK_RIGHT,
      "Enter" => VIRTUAL_KEY.VK_RETURN,
      "Esc" or "Escape" => VIRTUAL_KEY.VK_ESCAPE,
      "Alt" => VIRTUAL_KEY.VK_MENU,
      "Control" => VIRTUAL_KEY.VK_CONTROL,
      "Shift" => VIRTUAL_KEY.VK_SHIFT,
      "PAUSE" => VIRTUAL_KEY.VK_PAUSE,
      "BREAK" => VIRTUAL_KEY.VK_PAUSE,
      "Backspace" => VIRTUAL_KEY.VK_BACK,
      "Tab" => VIRTUAL_KEY.VK_TAB,
      "CapsLock" => VIRTUAL_KEY.VK_CAPITAL,
      "Delete" => VIRTUAL_KEY.VK_DELETE,
      "Home" => VIRTUAL_KEY.VK_HOME,
      "End" => VIRTUAL_KEY.VK_END,
      "PageUp" => VIRTUAL_KEY.VK_PRIOR,
      "PageDown" => VIRTUAL_KEY.VK_NEXT,
      "NumLock" => VIRTUAL_KEY.VK_NUMLOCK,
      "Insert" => VIRTUAL_KEY.VK_INSERT,
      "ScrollLock" => VIRTUAL_KEY.VK_SCROLL,
      "F1" => VIRTUAL_KEY.VK_F1,
      "F2" => VIRTUAL_KEY.VK_F2,
      "F3" => VIRTUAL_KEY.VK_F3,
      "F4" => VIRTUAL_KEY.VK_F4,
      "F5" => VIRTUAL_KEY.VK_F5,
      "F6" => VIRTUAL_KEY.VK_F6,
      "F7" => VIRTUAL_KEY.VK_F7,
      "F8" => VIRTUAL_KEY.VK_F8,
      "F9" => VIRTUAL_KEY.VK_F9,
      "F10" => VIRTUAL_KEY.VK_F10,
      "F11" => VIRTUAL_KEY.VK_F11,
      "F12" => VIRTUAL_KEY.VK_F12,
      "Meta" => VIRTUAL_KEY.VK_LWIN,
      "ContextMenu" => VIRTUAL_KEY.VK_MENU,
      "Hankaku" => VIRTUAL_KEY.VK_OEM_AUTO,
      "Hiragana" => VIRTUAL_KEY.VK_OEM_COPY,
      "KanaMode" => VIRTUAL_KEY.VK_KANA,
      "KanjiMode" => VIRTUAL_KEY.VK_KANJI,
      "Katakana" => VIRTUAL_KEY.VK_OEM_FINISH,
      "Romaji" => VIRTUAL_KEY.VK_OEM_BACKTAB,
      "Zenkaku" => VIRTUAL_KEY.VK_OEM_ENLW,
      _ => key.Length == 1
        ? (VIRTUAL_KEY)PInvoke.VkKeyScanEx(Convert.ToChar(key), GetKeyboardLayout())
        : null
    };

    if (result is not null)
    {
      return true;
    }

    _logger.LogWarning("Unable to parse key input: {key}.", key);
    return false;
  }

  private FrozenDictionary<HCURSOR, PointerCursor> GetCursorMap()
  {
    return _cursorMap ??= new Dictionary<HCURSOR, PointerCursor>
    {
      [PInvoke.LoadCursor(HINSTANCE.Null, (char*)32512)] = PointerCursor.NormalArrow,
      [PInvoke.LoadCursor(HINSTANCE.Null, (char*)32513)] = PointerCursor.Ibeam,
      [PInvoke.LoadCursor(HINSTANCE.Null, (char*)32514)] = PointerCursor.Wait,
      [PInvoke.LoadCursor(HINSTANCE.Null, (char*)32642)] = PointerCursor.SizeNwse,
      [PInvoke.LoadCursor(HINSTANCE.Null, (char*)32643)] = PointerCursor.SizeNesw,
      [PInvoke.LoadCursor(HINSTANCE.Null, (char*)32644)] = PointerCursor.SizeWe,
      [PInvoke.LoadCursor(HINSTANCE.Null, (char*)32645)] = PointerCursor.SizeNs,
      [PInvoke.LoadCursor(HINSTANCE.Null, (char*)32649)] = PointerCursor.Hand
    }.ToFrozenDictionary();
  }

  private void InvokeWheelScroll(int x, int y, int delta, bool isVertical)
  {
    var extraInfo = PInvoke.GetMessageExtraInfo();
    var mouseEventFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK | MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE;

    if (isVertical)
    {
      mouseEventFlags |= MOUSE_EVENT_FLAGS.MOUSEEVENTF_WHEEL;
    }
    else
    {
      mouseEventFlags |= MOUSE_EVENT_FLAGS.MOUSEEVENTF_HWHEEL;
    }

    var normalizedPoint = GetNormalizedPoint(x, y);

    var mouseInput = new MOUSEINPUT
    {
      dx = normalizedPoint.X,
      dy = normalizedPoint.Y,
      dwFlags = mouseEventFlags,
      mouseData = (uint)delta,
      dwExtraInfo = new nuint(extraInfo.Value.ToPointer())
    };

    var input = new INPUT
    {
      type = INPUT_TYPE.INPUT_MOUSE,
      Anonymous = { mi = mouseInput }
    };

    var result = PInvoke.SendInput([input], sizeof(INPUT));
    if (result == 0)
    {
      _logger.LogWarning("Failed to send mouse wheel input: {MouseInput}.", mouseInput);
    }
  }

  private void LogWin32Error([CallerMemberName] string? caller = null)
  {
    var lastErrMessage = Marshal.GetLastPInvokeErrorMessage();
    var lastErrCode = Marshal.GetLastPInvokeError();
    _logger.LogError(
      "Win32 error while calling {MemberName}.  " +
      "Error Code: {ErrorCode}.  Error Message: {ErrorMessage}",
      caller,
      lastErrCode,
      lastErrMessage);
  }

  private uint ResolveWindowsSession(int targetSessionId)
  {
    var activeSessions = GetActiveSessions();
    if (activeSessions.Any(x => x.SystemSessionId == targetSessionId))
    {
      // If exact match is found, return that session.
      return (uint)targetSessionId;
    }

    if (PInvoke.IsOS(OS.OS_ANYSERVER))
    {
      // If Windows Server, default to console session.
      return PInvoke.WTSGetActiveConsoleSessionId();
    }

    // If consumer version and there's an RDP session active, return that.
    if (activeSessions.Find(x => x.Type == DesktopSessionType.Rdp) is { } rdSession)
    {
      return (uint)rdSession.SystemSessionId;
    }

    // Otherwise, return the console session.
    return PInvoke.WTSGetActiveConsoleSessionId();
  }

  private bool TryCloneSystemPrimaryToken([NotNullWhen(true)] out SafeFileHandle? primaryToken)
  {
    primaryToken = null;

    try
    {
      var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
      var identity = WindowsIdentity.GetCurrent();
      if (identity.User?.Equals(systemSid) != true)
      {
        _logger.LogError("Current user is not Local System. Cannot clone system token.");
        return false;
      }

      var systemProcId = (uint)Environment.ProcessId;

      // Obtain a handle to the winlogon process;
      var explorerProcessHandle = PInvoke.OpenProcess(
        (PROCESS_ACCESS_RIGHTS)MaximumAllowedRights,
        true,
        systemProcId);

      // Obtain a handle to the access token of the winlogon process.
      using var explorerSafeProcHandle = new SafeProcessHandle(explorerProcessHandle, true);
      if (!PInvoke.OpenProcessToken(
            explorerSafeProcHandle,
            TOKEN_ACCESS_MASK.TOKEN_DUPLICATE,
            out var systemToken))
      {
        PInvoke.CloseHandle(explorerProcessHandle);
        return false;
      }

      // Copy the access token of the winlogon process; the newly created token will be a primary token.
      var result = PInvoke.DuplicateTokenEx(
        systemToken,
        (TOKEN_ACCESS_MASK)0xFFu,
        null,
        SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
        TOKEN_TYPE.TokenPrimary,
        out primaryToken);

      PInvoke.CloseHandle(explorerProcessHandle);
      systemToken.Close();
      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to duplicate Explorer token.  Last Win32 Error: {LastWin32Error}",
        Marshal.GetLastWin32Error());
      return false;
    }
  }
}