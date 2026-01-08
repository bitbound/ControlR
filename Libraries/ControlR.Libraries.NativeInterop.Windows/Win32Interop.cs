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
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.Security;
using Windows.Win32.System.StationsAndDesktops;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using WTS_CONNECTSTATE_CLASS = Windows.Win32.System.RemoteDesktop.WTS_CONNECTSTATE_CLASS;
using WTS_INFO_CLASS = Windows.Win32.System.RemoteDesktop.WTS_INFO_CLASS;
using WTS_SESSION_INFOW = Windows.Win32.System.RemoteDesktop.WTS_SESSION_INFOW;
using ControlR.Libraries.Shared.Dtos.RemoteControlDtos;

namespace ControlR.Libraries.NativeInterop.Windows;

public interface IWin32Interop
{
  bool CreateInteractiveSystemProcess(
    string commandLine,
    int targetSessionId,
    bool hiddenWindow,
    [NotNullWhen(true)] out Process? startedProcess);

  nint CreatePrivacyScreenWindow(int left, int top, int width, int height);
  void DestroyPrivacyScreenWindow(nint windowHandle);
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
  void InvokeKeyEvent(string key, string? code, bool isPressed);
  void InvokeMouseButtonEvent(int x, int y, int button, bool isPressed);
  void InvokeWheelScroll(int x, int y, int scrollY, int scrollX);
  void MovePointer(int x, int y, MovePointerType moveType);
  nint OpenInputDesktop();
  void ResetKeyboardState();
  bool SetBlockInput(bool isBlocked);
  void SetClipboardText(string? text);
  nint SetParentWindow(nint windowHandle, nint parentWindowHandle);
  bool SetWindowDisplayAffinity(nint windowHandle, WindowDisplayAffinity affinity);
  bool SetWindowPos(nint mainWindowHandle, nint insertAfter, int x, int y, int width, int height);

  bool StartProcessInBackgroundSession(
    string commandLine,
    bool hiddenWindow,
    [NotNullWhen(true)] out Process? startedProcess);

  bool SwitchToInputDesktop();
  void TypeText(string text);
}

[SupportedOSPlatform("windows6.1")]
public unsafe partial class Win32Interop(ILogger<Win32Interop> logger) : IWin32Interop
{
  private const uint CF_UNICODETEXT = 13u;
  private const uint GenericAllRights = 0x10000000u;
  private const uint MaximumAllowedRights = 0x2000000u;
  private const string PrivacyWindowClassName = "ControlR_PrivacyScreen";
  private const uint WM_MOUSEACTIVATE = 0x0021;
  private const uint WM_NCHITTEST = 0x0084;
  private const int WM_SAS_INTERNAL = 0x0208;
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
  private readonly Lock _windowClassLock = new();

  private FrozenDictionary<HCURSOR, PointerCursor>? _cursorMap;
  private HDESK _lastInputDesktop;
  private ushort _privacyWindowClassAtom;
  private WNDPROC? _privacyWindowProc;

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

      var desktopPtr = Marshal.StringToHGlobalAuto("winsta0\\Default\0");
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

  public nint CreatePrivacyScreenWindow(int left, int top, int width, int height)
  {
    EnsurePrivacyWindowClassRegistered();

    var windowHandle = PInvoke.CreateWindowEx(
      WINDOW_EX_STYLE.WS_EX_TRANSPARENT |
      WINDOW_EX_STYLE.WS_EX_TOPMOST |
      WINDOW_EX_STYLE.WS_EX_TOOLWINDOW |
      WINDOW_EX_STYLE.WS_EX_NOACTIVATE |
      WINDOW_EX_STYLE.WS_EX_LAYERED,
      PrivacyWindowClassName,
      "Privacy Screen",
      WINDOW_STYLE.WS_POPUP | WINDOW_STYLE.WS_VISIBLE,
      left,
      top,
      width,
      height,
      default,
      default,
      default,
      null);

    if (windowHandle.IsNull)
    {
      LogWin32Error();
      _logger.LogError("Failed to create privacy screen window.");
      return nint.Zero;
    }

    if (!PInvoke.SetLayeredWindowAttributes(windowHandle, new COLORREF(0), 255, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA))
    {
      LogWin32Error();
    }

    if (!SetWindowDisplayAffinity(windowHandle, WindowDisplayAffinity.ExcludeFromCapture))
    {
      PInvoke.DestroyWindow(windowHandle);
      return nint.Zero;
    }

    return windowHandle;
  }

  public void DestroyPrivacyScreenWindow(nint windowHandle)
  {
    if (windowHandle == nint.Zero)
    {
      return;
    }

    SetWindowDisplayAffinity(windowHandle, WindowDisplayAffinity.None);
    PInvoke.DestroyWindow(new HWND(windowHandle));
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

      var data = PInvoke.GetClipboardData(CF_UNICODETEXT);
      return Marshal.PtrToStringUni(data);
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
    try
    {
      var currentSessionId = Process.GetCurrentProcess().SessionId;

      if (currentSessionId == 0)
      {
        PInvoke.SendSAS(false);
      }
      else
      {
        var ptrParam = Marshal.AllocHGlobal(1);
        Marshal.WriteByte(ptrParam, 0);
        var result = Wmsgapi.WmsgSendMessage(currentSessionId, WM_SAS_INTERNAL, 0, ptrParam);
        if (result != 0)
        {
          _logger.LogWarning("Failed to send Ctrl+Alt+Del message to session {SessionId}. Result: {Result}",
            currentSessionId,
            result);
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while invoking Ctrl+Alt+Del.");
    }
  }

  public void InvokeKeyEvent(string key, string? code, bool isPressed)
  {
    if (!ConvertBrowserKeyArgToVirtualKey(key, code, out var convertResult))
    {
      _logger.LogWarning("Failed to convert key '{Key}' (code: '{Code}') to virtual key.", key, code);
      return;
    }

    var kbdInput = CreateKeyboardInput(convertResult.Value, isPressed, code);

    var result = PInvoke.SendInput([kbdInput], sizeof(INPUT));

    if (result == 0)
    {
      _logger.LogWarning("Failed to send key input. Key: {Key}, Code: {Code}, IsPressed: {IsPressed}.", key, code, isPressed);
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
        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
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

        var state = PInvoke.GetAsyncKeyState((int)key);

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
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while resetting key state for key {Key}.", key);
      }
    }

    if (inputs.Count > 0)
    {
      _logger.LogDebug("Releasing {Count} stuck keys during keyboard state reset", inputs.Count);
    }
    else
    {
      _logger.LogDebug("No stuck keys found during keyboard state reset");
    }

    foreach (var input in inputs)
    {
      try
      {
        var result = PInvoke.SendInput([input], sizeof(INPUT));
        if (result != 1)
        {
          _logger.LogWarning("Failed to reset key state.");
        }

        Thread.Sleep(1);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while sending reset key input.");
      }
    }
  }

  public bool SetBlockInput(bool isBlocked)
  {
    if (PInvoke.BlockInput(isBlocked))
    {
      return true;
    }

    _logger.LogError("Failed to set input block state to {IsBlocked}.", isBlocked);
    LogWin32Error();
    return false;
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

  [SupportedOSPlatform("windows6.1")]
  public bool SetWindowDisplayAffinity(nint windowHandle, WindowDisplayAffinity affinity)
  {
    var hwnd = new HWND(windowHandle);
    if (PInvoke.SetWindowDisplayAffinity(hwnd, (WINDOW_DISPLAY_AFFINITY)affinity))
    {
      return true;
    }

    _logger.LogError("Failed to set window display affinity to {Affinity} for window {WindowHandle}.", affinity, windowHandle);
    LogWin32Error();
    return false;
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

  private static INPUT CreateKeyboardInput(VIRTUAL_KEY key, bool isPressed, string? code = null)
  {
    var extraInfo = PInvoke.GetMessageExtraInfo();
    KEYBD_EVENT_FLAGS kbdFlags = 0;

    if (IsExtendedKey(key, code))
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
      wScan = 0, // Not used for VK-based injection (would need KEYEVENTF_SCANCODE flag)
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

  /// <summary>
  /// Creates a scancode-based keyboard input for physical key simulation.
  /// TODO: This will be used when physical input mode is enabled on the viewer side.
  /// </summary>
  /// <param name="scancode">The hardware scancode for the physical key</param>
  /// <param name="isPressed">True for key down, false for key up</param>
  /// <param name="isExtended">True if this is an extended scancode (E0 prefix)</param>
  /// <returns>INPUT structure for scancode-based injection</returns>
  private static INPUT CreateScancodeInput(ushort scancode, bool isPressed, bool isExtended = false)
  {
    var extraInfo = PInvoke.GetMessageExtraInfo();
    KEYBD_EVENT_FLAGS kbdFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_SCANCODE;

    if (isExtended)
    {
      kbdFlags |= KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY;
    }

    if (!isPressed)
    {
      kbdFlags |= KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
    }

    var kbdInput = new KEYBDINPUT
    {
      wVk = 0, // Not used for scancode injection
      wScan = scancode,
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
    var left = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN);
    var top = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN);
    var width = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
    var height = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);

    var normalizedX = (x - left) * 65535 / width;
    var normalizedY = (y - top) * 65535 / height;

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

  private static bool IsExtendedKey(VIRTUAL_KEY vk, string? code = null)
  {
    // Special handling for VK_RETURN: only NumpadEnter should be extended
    if (vk == VIRTUAL_KEY.VK_RETURN)
    {
      // If we have code, check if it's NumpadEnter
      if (!string.IsNullOrEmpty(code))
      {
        return code == "NumpadEnter";
      }
      // Mobile fallback: assume regular Enter (non-extended) when code is missing
      return false;
    }

    return vk switch
    {
      // Right-side modifiers are extended (left-side are not)
      VIRTUAL_KEY.VK_RCONTROL or
        VIRTUAL_KEY.VK_RMENU or
        // Navigation cluster keys (not numpad)
        VIRTUAL_KEY.VK_INSERT or
        VIRTUAL_KEY.VK_DELETE or
        VIRTUAL_KEY.VK_HOME or
        VIRTUAL_KEY.VK_END or
        VIRTUAL_KEY.VK_PRIOR or
        VIRTUAL_KEY.VK_NEXT or
        // Arrow keys
        VIRTUAL_KEY.VK_LEFT or
        VIRTUAL_KEY.VK_UP or
        VIRTUAL_KEY.VK_RIGHT or
        VIRTUAL_KEY.VK_DOWN or
        // Other extended keys
        VIRTUAL_KEY.VK_NUMLOCK or
        VIRTUAL_KEY.VK_CANCEL or
        VIRTUAL_KEY.VK_DIVIDE or
        VIRTUAL_KEY.VK_SNAPSHOT => true,
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

  private bool ConvertBrowserKeyArgToVirtualKey(string key, string? code, [NotNullWhen(true)] out VIRTUAL_KEY? result)
  {
    // Code-first approach (physical mode): Try to map browser KeyboardEvent.code to Windows Virtual Key
    // This provides layout-independent physical key simulation
    // When code is null, we skip this and use logical mode (key-based) instead
    if (!string.IsNullOrWhiteSpace(code))
    {
      result = code switch
      {
        // Letter keys (physical key position, layout-independent)
        "KeyA" => VIRTUAL_KEY.VK_A,
        "KeyB" => VIRTUAL_KEY.VK_B,
        "KeyC" => VIRTUAL_KEY.VK_C,
        "KeyD" => VIRTUAL_KEY.VK_D,
        "KeyE" => VIRTUAL_KEY.VK_E,
        "KeyF" => VIRTUAL_KEY.VK_F,
        "KeyG" => VIRTUAL_KEY.VK_G,
        "KeyH" => VIRTUAL_KEY.VK_H,
        "KeyI" => VIRTUAL_KEY.VK_I,
        "KeyJ" => VIRTUAL_KEY.VK_J,
        "KeyK" => VIRTUAL_KEY.VK_K,
        "KeyL" => VIRTUAL_KEY.VK_L,
        "KeyM" => VIRTUAL_KEY.VK_M,
        "KeyN" => VIRTUAL_KEY.VK_N,
        "KeyO" => VIRTUAL_KEY.VK_O,
        "KeyP" => VIRTUAL_KEY.VK_P,
        "KeyQ" => VIRTUAL_KEY.VK_Q,
        "KeyR" => VIRTUAL_KEY.VK_R,
        "KeyS" => VIRTUAL_KEY.VK_S,
        "KeyT" => VIRTUAL_KEY.VK_T,
        "KeyU" => VIRTUAL_KEY.VK_U,
        "KeyV" => VIRTUAL_KEY.VK_V,
        "KeyW" => VIRTUAL_KEY.VK_W,
        "KeyX" => VIRTUAL_KEY.VK_X,
        "KeyY" => VIRTUAL_KEY.VK_Y,
        "KeyZ" => VIRTUAL_KEY.VK_Z,

        // Digit keys (main keyboard row)
        "Digit0" => VIRTUAL_KEY.VK_0,
        "Digit1" => VIRTUAL_KEY.VK_1,
        "Digit2" => VIRTUAL_KEY.VK_2,
        "Digit3" => VIRTUAL_KEY.VK_3,
        "Digit4" => VIRTUAL_KEY.VK_4,
        "Digit5" => VIRTUAL_KEY.VK_5,
        "Digit6" => VIRTUAL_KEY.VK_6,
        "Digit7" => VIRTUAL_KEY.VK_7,
        "Digit8" => VIRTUAL_KEY.VK_8,
        "Digit9" => VIRTUAL_KEY.VK_9,

        // Numpad keys
        "Numpad0" => VIRTUAL_KEY.VK_NUMPAD0,
        "Numpad1" => VIRTUAL_KEY.VK_NUMPAD1,
        "Numpad2" => VIRTUAL_KEY.VK_NUMPAD2,
        "Numpad3" => VIRTUAL_KEY.VK_NUMPAD3,
        "Numpad4" => VIRTUAL_KEY.VK_NUMPAD4,
        "Numpad5" => VIRTUAL_KEY.VK_NUMPAD5,
        "Numpad6" => VIRTUAL_KEY.VK_NUMPAD6,
        "Numpad7" => VIRTUAL_KEY.VK_NUMPAD7,
        "Numpad8" => VIRTUAL_KEY.VK_NUMPAD8,
        "Numpad9" => VIRTUAL_KEY.VK_NUMPAD9,
        "NumpadMultiply" => VIRTUAL_KEY.VK_MULTIPLY,
        "NumpadAdd" => VIRTUAL_KEY.VK_ADD,
        "NumpadSubtract" => VIRTUAL_KEY.VK_SUBTRACT,
        "NumpadDecimal" => VIRTUAL_KEY.VK_DECIMAL,
        "NumpadDivide" => VIRTUAL_KEY.VK_DIVIDE,
        "NumpadEnter" => VIRTUAL_KEY.VK_RETURN,

        // Function keys
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
        "F13" => VIRTUAL_KEY.VK_F13,
        "F14" => VIRTUAL_KEY.VK_F14,
        "F15" => VIRTUAL_KEY.VK_F15,
        "F16" => VIRTUAL_KEY.VK_F16,
        "F17" => VIRTUAL_KEY.VK_F17,
        "F18" => VIRTUAL_KEY.VK_F18,
        "F19" => VIRTUAL_KEY.VK_F19,
        "F20" => VIRTUAL_KEY.VK_F20,
        "F21" => VIRTUAL_KEY.VK_F21,
        "F22" => VIRTUAL_KEY.VK_F22,
        "F23" => VIRTUAL_KEY.VK_F23,
        "F24" => VIRTUAL_KEY.VK_F24,

        // Navigation keys
        "ArrowDown" => VIRTUAL_KEY.VK_DOWN,
        "ArrowUp" => VIRTUAL_KEY.VK_UP,
        "ArrowLeft" => VIRTUAL_KEY.VK_LEFT,
        "ArrowRight" => VIRTUAL_KEY.VK_RIGHT,
        "Home" => VIRTUAL_KEY.VK_HOME,
        "End" => VIRTUAL_KEY.VK_END,
        "PageUp" => VIRTUAL_KEY.VK_PRIOR,
        "PageDown" => VIRTUAL_KEY.VK_NEXT,

        // Editing keys
        "Backspace" => VIRTUAL_KEY.VK_BACK,
        "Tab" => VIRTUAL_KEY.VK_TAB,
        "Enter" => VIRTUAL_KEY.VK_RETURN,
        "Delete" => VIRTUAL_KEY.VK_DELETE,
        "Insert" => VIRTUAL_KEY.VK_INSERT,

        // Modifier keys
        "ShiftLeft" => VIRTUAL_KEY.VK_LSHIFT,
        "ShiftRight" => VIRTUAL_KEY.VK_RSHIFT,
        "ControlLeft" => VIRTUAL_KEY.VK_LCONTROL,
        "ControlRight" => VIRTUAL_KEY.VK_RCONTROL,
        "AltLeft" => VIRTUAL_KEY.VK_LMENU,
        "AltRight" => VIRTUAL_KEY.VK_RMENU,
        "MetaLeft" => VIRTUAL_KEY.VK_LWIN,
        "MetaRight" => VIRTUAL_KEY.VK_RWIN,

        // Lock keys
        "CapsLock" => VIRTUAL_KEY.VK_CAPITAL,
        "NumLock" => VIRTUAL_KEY.VK_NUMLOCK,
        "ScrollLock" => VIRTUAL_KEY.VK_SCROLL,

        // Special keys
        "Escape" => VIRTUAL_KEY.VK_ESCAPE,
        "Space" => VIRTUAL_KEY.VK_SPACE,
        "Pause" => VIRTUAL_KEY.VK_PAUSE,
        "ContextMenu" => VIRTUAL_KEY.VK_APPS,
        "PrintScreen" => VIRTUAL_KEY.VK_SNAPSHOT,

        // OEM/Punctuation keys (US layout physical positions)
        "Semicolon" => VIRTUAL_KEY.VK_OEM_1,      // ;: key
        "Equal" => VIRTUAL_KEY.VK_OEM_PLUS,        // =+ key
        "Comma" => VIRTUAL_KEY.VK_OEM_COMMA,       // ,< key
        "Minus" => VIRTUAL_KEY.VK_OEM_MINUS,       // -_ key
        "Period" => VIRTUAL_KEY.VK_OEM_PERIOD,     // .> key
        "Slash" => VIRTUAL_KEY.VK_OEM_2,           // /? key
        "Backquote" => VIRTUAL_KEY.VK_OEM_3,       // `~ key
        "BracketLeft" => VIRTUAL_KEY.VK_OEM_4,     // [{ key
        "Backslash" => VIRTUAL_KEY.VK_OEM_5,       // \| key
        "BracketRight" => VIRTUAL_KEY.VK_OEM_6,    // ]} key
        "Quote" => VIRTUAL_KEY.VK_OEM_7,           // '" key
        "IntlBackslash" => VIRTUAL_KEY.VK_OEM_102, // <> key (non-US keyboards)

        // Media keys
        "AudioVolumeUp" => VIRTUAL_KEY.VK_VOLUME_UP,
        "AudioVolumeDown" => VIRTUAL_KEY.VK_VOLUME_DOWN,
        "AudioVolumeMute" => VIRTUAL_KEY.VK_VOLUME_MUTE,
        "MediaTrackNext" => VIRTUAL_KEY.VK_MEDIA_NEXT_TRACK,
        "MediaTrackPrevious" => VIRTUAL_KEY.VK_MEDIA_PREV_TRACK,
        "MediaStop" => VIRTUAL_KEY.VK_MEDIA_STOP,
        "MediaPlayPause" => VIRTUAL_KEY.VK_MEDIA_PLAY_PAUSE,

        // Browser keys
        "BrowserBack" => VIRTUAL_KEY.VK_BROWSER_BACK,
        "BrowserForward" => VIRTUAL_KEY.VK_BROWSER_FORWARD,
        "BrowserRefresh" => VIRTUAL_KEY.VK_BROWSER_REFRESH,
        "BrowserStop" => VIRTUAL_KEY.VK_BROWSER_STOP,
        "BrowserSearch" => VIRTUAL_KEY.VK_BROWSER_SEARCH,
        "BrowserFavorites" => VIRTUAL_KEY.VK_BROWSER_FAVORITES,
        "BrowserHome" => VIRTUAL_KEY.VK_BROWSER_HOME,

        // Japanese/Korean IME keys
        "Convert" => VIRTUAL_KEY.VK_CONVERT,
        "NonConvert" => VIRTUAL_KEY.VK_NONCONVERT,
        "KanaMode" => VIRTUAL_KEY.VK_KANA,
        "KanjiMode" => VIRTUAL_KEY.VK_KANJI,
        "Lang1" => VIRTUAL_KEY.VK_HANGUL,
        "Lang2" => VIRTUAL_KEY.VK_HANJA,

        _ => null
      };

      if (result is not null)
      {
        return true;
      }
    }

    // Fallback to key-based mapping for compatibility with older code or edge cases
    // This handles cases where code is not provided (shouldn't happen in modern browsers)
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
      "ContextMenu" => VIRTUAL_KEY.VK_APPS,
      "Hankaku" => VIRTUAL_KEY.VK_OEM_AUTO,
      "Hiragana" => VIRTUAL_KEY.VK_OEM_COPY,
      "KanaMode" => VIRTUAL_KEY.VK_KANA,
      "KanjiMode" => VIRTUAL_KEY.VK_KANJI,
      "Katakana" => VIRTUAL_KEY.VK_OEM_FINISH,
      "Romaji" => VIRTUAL_KEY.VK_OEM_BACKTAB,
      "Zenkaku" => VIRTUAL_KEY.VK_OEM_ENLW,
      _ => null
    };

    if (result is not null)
    {
      return true;
    }

    // Final fallback: Use VkKeyScanEx for single-character keys
    // This is mainly for edge cases and ensures we can handle any printable character
    if (key.Length == 1)
    {
      var scanResult = PInvoke.VkKeyScanEx(Convert.ToChar(key), GetKeyboardLayout());

      // VkKeyScanEx returns -1 if the key is not found in the current keyboard layout
      if (scanResult == -1)
      {
        _logger.LogWarning("Key '{Key}' (code: '{Code}') not found in current keyboard layout.", key, code);
        return false;
      }

      // Mask to get only the low byte (virtual key code), ignoring the shift state in the high byte
      result = (VIRTUAL_KEY)(scanResult & 0xFF);
      return true;
    }

    _logger.LogWarning("Unable to parse key input: {Key} (code: {Code}).", key, code);
    return false;
  }

  private void EnsurePrivacyWindowClassRegistered()
  {
    using var lockScope = _windowClassLock.EnterScope();

    if (_privacyWindowClassAtom != 0)
    {
      return;
    }

    var blackBrush = PInvoke.CreateSolidBrush(new COLORREF(0));

    _privacyWindowProc = PrivacyWindowProc;

    var wndClass = new WNDCLASSEXW
    {
      cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
      style = WNDCLASS_STYLES.CS_HREDRAW | WNDCLASS_STYLES.CS_VREDRAW,
      lpfnWndProc = _privacyWindowProc,
      cbClsExtra = 0,
      cbWndExtra = 0,
      hInstance = HINSTANCE.Null,
      hIcon = HICON.Null,
      hCursor = HCURSOR.Null,
      hbrBackground = new HBRUSH((nint)blackBrush),
      lpszMenuName = default
    };

    unsafe
    {
      fixed (char* pClassName = PrivacyWindowClassName)
      {
        wndClass.lpszClassName = pClassName;
        wndClass.hIconSm = HICON.Null;

        _privacyWindowClassAtom = PInvoke.RegisterClassEx(wndClass);
      }
    }

    if (_privacyWindowClassAtom == 0)
    {
      _logger.LogError("Failed to register privacy window class.");
      LogWin32Error();
    }
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

  private LRESULT PrivacyWindowProc(HWND hwnd, uint uMsg, WPARAM wParam, LPARAM lParam)
  {
    // Ensure the privacy screen window never intercepts pointer input.
    // WM_NCHITTEST determines which window should receive mouse events; returning HTTRANSPARENT
    // causes the system to continue hit-testing windows underneath.
    if (uMsg == WM_NCHITTEST)
    {
      return new LRESULT(-1); // HTTRANSPARENT
    }

    // Extra guard: never activate the window if it is somehow targeted.
    if (uMsg == WM_MOUSEACTIVATE)
    {
      return new LRESULT(3); // MA_NOACTIVATE
    }

    return PInvoke.DefWindowProc(hwnd, uMsg, wParam, lParam);
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