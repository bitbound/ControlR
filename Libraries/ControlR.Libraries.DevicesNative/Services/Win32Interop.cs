using ControlR.Libraries.DevicesNative.Windows;
using ControlR.Libraries.Shared.Dtos.SidecarDtos;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.StationsAndDesktops;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using WTS_CONNECTSTATE_CLASS = global::Windows.Win32.System.RemoteDesktop.WTS_CONNECTSTATE_CLASS;
using WTS_INFO_CLASS = global::Windows.Win32.System.RemoteDesktop.WTS_INFO_CLASS;
using WTS_SESSION_INFOW = global::Windows.Win32.System.RemoteDesktop.WTS_SESSION_INFOW;

namespace ControlR.Devices.Native.Services;

public interface IWin32Interop
{
    bool CreateInteractiveSystemProcess(string commandLine, int targetSessionId, bool forceConsoleSession, string desktopName, bool hiddenWindow, out Process? startedProcess);
    IEnumerable<WindowsSession> GetActiveSessions();
    IEnumerable<WindowsSession> GetActiveSessionsCsWin32();
    bool GetCurrentThreadDesktop(out string desktopName);
    bool GetInputDesktop(out string desktopName);
    bool GetThreadDesktop(uint threadId, out string desktopName);
    string GetUsernameFromSessionId(uint sessionId);
    bool GlobalMemoryStatus(ref MemoryStatusEx lpBuffer);
    void InvokeCtrlAltDel();
    Result InvokeKeyEvent(string key, bool isPressed);
    void InvokeMouseButtonEvent(int x, int y, int button, bool isPressed);
    void InvokeWheelScroll(int x, int y, int scrollY, int scrollX);
    void MovePointer(int x, int y, MovePointerType moveType);
    nint OpenInputDesktop();
    void ResetKeyboardState();
    bool SwitchToInputDesktop();
    void TypeText(string text);
}

[SupportedOSPlatform("windows6.0.6000")]
public unsafe partial class Win32Interop(ILogger<Win32Interop> _logger) : IWin32Interop
{
    private const uint MAXIMUM_ALLOWED_RIGHTS = 0x2000000;
    private const string SE_SECURITY_NAME = "SeSecurityPrivilege\0";
    private const uint XBUTTON1 = 0x0001;
    private const uint XBUTTON2 = 0x0002;
    private FrozenDictionary<string, ushort>? _keyMap;
    private HDESK _lastInputDesktop;

    [Flags]
    private enum ShiftState : byte
    {
        None = 0,
        ShiftPressed = 1 << 0,
        CtrlPressed = 1 << 1,
        AltPressed = 1 << 2,
        HankakuPressed = 1 << 3,
        Reserved1 = 1 << 4,
        Reserved2 = 1 << 5,
    }

    public bool CreateInteractiveSystemProcess(
        string commandLine,
        int targetSessionId,
        bool forceConsoleSession,
        string desktopName,
        bool hiddenWindow,
        out Process? startedProcess)
    {
        startedProcess = null;
        try
        {
            uint winLogonPid = 0;

            // If not force console, find target session.  If not present,
            // use last active session.
            var sessionId = GetTargetSessionId(targetSessionId, forceConsoleSession);

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
                (PROCESS_ACCESS_RIGHTS)MAXIMUM_ALLOWED_RIGHTS,
                true,
                winLogonPid);

            // Obtain a handle to the access token of the winlogon process.
            using var winLogonSafeProcHandle = new SafeProcessHandle(winLogonProcessHandle.Value, true);
            if (!PInvoke.OpenProcessToken(winLogonSafeProcHandle, TOKEN_ACCESS_MASK.TOKEN_DUPLICATE, out var winLogonToken))
            {
                _logger.LogWarning("Failed to open winlogon process.");
                PInvoke.CloseHandle(winLogonProcessHandle);
                return false;
            }

            // Security attibute structure used in DuplicateTokenEx and CreateProcessAsUser.
            var securityAttributes = new SECURITY_ATTRIBUTES
            {
                nLength = (uint)sizeof(SECURITY_ATTRIBUTES)
            };

            // Copy the access token of the winlogon process; the newly created token will be a primary token.
            if (!PInvoke.DuplicateTokenEx(
                winLogonToken,
                (TOKEN_ACCESS_MASK)MAXIMUM_ALLOWED_RIGHTS,
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
            var desktopPtr = Marshal.StringToHGlobalAuto($"winsta0\\{desktopName}\0");
            startupInfo.lpDesktop = new PWSTR((char*)desktopPtr.ToPointer());

            // Flags that specify the priority and creation method of the process.
            var dwCreationFlags = PROCESS_CREATION_FLAGS.NORMAL_PRIORITY_CLASS | PROCESS_CREATION_FLAGS.CREATE_UNICODE_ENVIRONMENT;

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
            _logger.LogError(ex, "Error while starting interactive system process.  Last Win32 error: {LastWin32Error}", lastWin32);
            return false;
        }
    }

    public IEnumerable<WindowsSession> GetActiveSessions()
    {
        var sessions = new List<WindowsSession>();
        var consoleSessionId = PInvoke.WTSGetActiveConsoleSessionId();
        sessions.Add(new WindowsSession()
        {
            Id = consoleSessionId,
            Type = WindowsSessionType.Console,
            Name = "Console",
            Username = GetUsernameFromSessionId(consoleSessionId)
        });

        nint ppSessionInfo = nint.Zero;
        var count = 0;
        var enumSessionResult = WtsApi32.WTSEnumerateSessions(WtsApi32.WTS_CURRENT_SERVER_HANDLE, 0, 1, ref ppSessionInfo, ref count);
        var dataSize = Marshal.SizeOf<WtsApi32.WTS_SESSION_INFO>();
        var current = ppSessionInfo;

        if (enumSessionResult == 0)
        {
            return sessions;
        }

        for (int i = 0; i < count; i++)
        {
            var wtsInfoObj = Marshal.PtrToStructure(current, typeof(WtsApi32.WTS_SESSION_INFO));
            if (wtsInfoObj is not WtsApi32.WTS_SESSION_INFO sessionInfo)
            {
                continue;
            }

            current += dataSize;
            if (sessionInfo.State == WtsApi32.WTS_CONNECTSTATE_CLASS.WTSActive && sessionInfo.SessionID != consoleSessionId)
            {

                sessions.Add(new WindowsSession()
                {
                    Id = sessionInfo.SessionID,
                    Name = sessionInfo.pWinStationName,
                    Type = WindowsSessionType.RDP,
                    Username = GetUsernameFromSessionId(sessionInfo.SessionID)
                });
            }
        }

        WtsApi32.WTSFreeMemory(ppSessionInfo);

        return sessions;
    }

    public IEnumerable<WindowsSession> GetActiveSessionsCsWin32()
    {
        var sessions = new List<WindowsSession>();

        unsafe
        {
            var consoleSessionId = PInvoke.WTSGetActiveConsoleSessionId();
            sessions.Add(new WindowsSession()
            {
                Id = consoleSessionId,
                Type = WindowsSessionType.Console,
                Name = "Console",
                Username = GetUsernameFromSessionId(consoleSessionId)
            });


            var enumSessionResult = PInvoke.WTSEnumerateSessions(
                HANDLE.WTS_CURRENT_SERVER_HANDLE,
                Reserved: 0,
                Version: 1,
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
                    sessions.Add(new WindowsSession()
                    {
                        Id = ppSessionInfos->SessionId,
                        Name = ppSessionInfos->pWinStationName.ToString(),
                        Type = WindowsSessionType.RDP,
                        Username = GetUsernameFromSessionId(ppSessionInfos->SessionId)
                    });
                }
                ppSessionInfos += dataSize;
            }
            PInvoke.WTSFreeMemory(ppSessionInfos);
        }

        return [.. sessions];
    }

    public bool GetCurrentThreadDesktop(out string desktopName)
    {
        var threadId = PInvoke.GetCurrentThreadId();
        return GetThreadDesktop(threadId, out desktopName);
    }

    public bool GetInputDesktop(out string desktopName)
    {
        var inputDesktop = GetInputDesktop();
        if (inputDesktop.IsNull)
        {
            desktopName = string.Empty;
            return false;
        }

        return GetDesktopName(inputDesktop, out desktopName);
    }

    public bool GetThreadDesktop(uint threadId, out string desktopName)
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
        var result = PInvoke.WTSQuerySessionInformation(HANDLE.Null, sessionId, WTS_INFO_CLASS.WTSUserName, out var username, out var bytesReturned);

        if (result && bytesReturned > 1)
        {
            return username.ToString();
        }

        return string.Empty;
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

    public Result InvokeKeyEvent(string key, bool isPressed)
    {
        if (!ConvertJavaScriptKeyToVirtualKey(key, out var convertResult))
        {
            return Result.Fail("Failed to convert key to virtual key.");
        }

        var kbdInput = CreateKeyboardInput(convertResult.Value, isPressed);

        var result = PInvoke.SendInput([kbdInput], sizeof(INPUT));

        if (result == 0)
        {
            _logger.LogWarning("Failed to send key input. Key: {Key}, IsPressed: {IsPressed}.", key, isPressed);
            return Result.Fail("Failed to send key input.");
        }

        return Result.Ok();
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
                mouseData = XBUTTON1;
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
                mouseData = XBUTTON2;
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
            dwExtraInfo = new nuint(extraInfo.Value.ToPointer()),
        };

        var input = new INPUT()
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
        return PInvoke.OpenInputDesktop(0, true, (DESKTOP_ACCESS_FLAGS)0x10000000u).Value;
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
                    default:
                        break;
                }

                if (state != 0)
                {
                    var kbdInput = new KEYBDINPUT()
                    {
                        wVk = key,
                        dwExtraInfo = new nuint(extraInfo.Value.ToPointer()),
                        dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP
                    };

                    var input = new INPUT()
                    {
                        type = INPUT_TYPE.INPUT_KEYBOARD,
                        Anonymous = { ki = kbdInput }
                    };

                    inputs.Add(input);
                }

            }
            catch { }
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

            var result = PInvoke.SetThreadDesktop(inputDesktop) && PInvoke.SwitchDesktop(inputDesktop);
            _lastInputDesktop = inputDesktop;
            return result;
        }
        catch
        {
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

            var down = new INPUT()
            {
                type = INPUT_TYPE.INPUT_KEYBOARD,
                Anonymous =
                {
                    ki = new KEYBDINPUT()
                    {
                        wVk = 0,
                        wScan = scanCode,
                        dwFlags = flags,
                        dwExtraInfo = new nuint(PInvoke.GetMessageExtraInfo().Value.ToPointer()),
                        time = 0,
                    }
                }
            };

            var up = new INPUT()
            {
                type = INPUT_TYPE.INPUT_KEYBOARD,
                Anonymous =
                {
                    ki = new KEYBDINPUT()
                    {
                        wVk = 0,
                        wScan = scanCode,
                        dwFlags = flags | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
                        dwExtraInfo = new nuint(PInvoke.GetMessageExtraInfo().Value.ToPointer()),
                        time = 0,
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

    private static void AddShiftInput(List<INPUT> inputs, ShiftState shiftState, bool isPressed)
    {
        switch (shiftState)
        {
            case ShiftState.ShiftPressed:
                {
                    inputs.Add(CreateKeyboardInput(VIRTUAL_KEY.VK_SHIFT, isPressed));
                    break;
                }
            case ShiftState.CtrlPressed:
                {
                    inputs.Add(CreateKeyboardInput(VIRTUAL_KEY.VK_CONTROL, isPressed));
                    break;
                }
            case ShiftState.AltPressed:
                {
                    inputs.Add(CreateKeyboardInput(VIRTUAL_KEY.VK_MENU, isPressed));
                    break;
                }
            case ShiftState.HankakuPressed:
            case ShiftState.None:
            case ShiftState.Reserved1:
            case ShiftState.Reserved2:
            default:
                break;
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

        var kbdInput = new KEYBDINPUT()
        {
            wVk = key,
            wScan = (ushort)(PInvoke.MapVirtualKeyEx((uint)key, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC_EX, GetKeyboardLayout())),
            dwExtraInfo = new nuint(extraInfo.Value.ToPointer()),
            dwFlags = kbdFlags,
            time = 0
        };

        var input = new INPUT()
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

        if (!PInvoke.GetUserObjectInformation(deskHandle, USER_OBJECT_INFORMATION_INDEX.UOI_NAME, outValue.ToPointer(), 256, (uint*)outLength.ToPointer()))
        {
            desktopName = string.Empty;
            return false;
        }

        desktopName = Marshal.PtrToStringAuto(outValue)?.Trim() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(desktopName);
    }

    private static HDESK GetInputDesktop()
    {
        return PInvoke.OpenInputDesktop(0, true, (DESKTOP_ACCESS_FLAGS)0x10000000u);
    }

    private static HKL GetKeyboardLayout()
    {
        return PInvoke.GetKeyboardLayout((uint)Environment.CurrentManagedThreadId);
    }

    private static VIRTUAL_KEY[] GetModKeysPressed()
    {
        var keys = new List<VIRTUAL_KEY>();

        var code = PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_SHIFT);
        var shortHelper = new ShortHelper(code);
        if (shortHelper.High == 255)
        {
            keys.Add(VIRTUAL_KEY.VK_SHIFT);
        }

        code = PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_CONTROL);
        shortHelper = new ShortHelper(code);
        if (shortHelper.High == 255)
        {
            keys.Add(VIRTUAL_KEY.VK_CONTROL);
        }

        code = PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_MENU);
        shortHelper = new ShortHelper(code);
        if (shortHelper.High == 255)
        {
            keys.Add(VIRTUAL_KEY.VK_MENU);
        }

        return [.. keys];
    }

    private static Point GetNormalizedPoint(int x, int y)
    {
        var width = (double)PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
        var height = (double)PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);

        return new Point((int)(x / width * 65535), (int)(y / height * 65535));
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
            _ => false,
        };
    }

    private bool ConvertJavaScriptKeyToVirtualKey(string key, [NotNullWhen(true)] out VIRTUAL_KEY? result)
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
            _ => key.Length == 1 ?
                    (VIRTUAL_KEY)PInvoke.VkKeyScanEx(Convert.ToChar(key), GetKeyboardLayout()) :
                    null
        };

        if (result is null)
        {
            _logger.LogWarning("Unable to parse key input: {key}.", key);
            return false;
        }
        return true;
    }

    private INPUT GetPointerMoveInput(int x, int y, MovePointerType moveType)
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
            dwExtraInfo = new nuint(extraInfo.Value.ToPointer()),
        };

        return new INPUT()
        {
            type = INPUT_TYPE.INPUT_MOUSE,
            Anonymous = { mi = mouseInput }
        };
    }
    private FrozenDictionary<string, ushort> GetScanCodeKeyMap()
    {
        return _keyMap ??= new Dictionary<string, ushort>()
        {
            ["Escape"] = 0x0001,
            ["Digit1"] = 0x0002,
            ["Digit2"] = 0x0003,
            ["Digit3"] = 0x0004,
            ["Digit4"] = 0x0005,
            ["Digit5"] = 0x0006,
            ["Digit6"] = 0x0007,
            ["Digit7"] = 0x0008,
            ["Digit8"] = 0x0009,
            ["Digit9"] = 0x000a,
            ["Digit0"] = 0x000b,
            ["Minus"] = 0x000c,
            ["Equal"] = 0x000d,
            ["Backspace"] = 0x000e,
            ["Tab"] = 0x000f,
            ["KeyQ"] = 0x0010,
            ["KeyW"] = 0x0011,
            ["KeyE"] = 0x0012,
            ["KeyR"] = 0x0013,
            ["KeyT"] = 0x0014,
            ["KeyY"] = 0x0015,
            ["KeyU"] = 0x0016,
            ["KeyI"] = 0x0017,
            ["KeyO"] = 0x0018,
            ["KeyP"] = 0x0019,
            ["BracketLeft"] = 0x001a,
            ["BracketRight"] = 0x001b,
            ["Enter"] = 0x001c,
            ["ControlLeft"] = 0x001d,
            ["KeyA"] = 0x001e,
            ["KeyS"] = 0x001f,
            ["KeyD"] = 0x0020,
            ["KeyF"] = 0x0021,
            ["KeyG"] = 0x0022,
            ["KeyH"] = 0x0023,
            ["KeyJ"] = 0x0024,
            ["KeyK"] = 0x0025,
            ["KeyL"] = 0x0026,
            ["Semicolon"] = 0x0027,
            ["Quote"] = 0x0028,
            ["Backquote"] = 0x0029,
            ["ShiftLeft"] = 0x002a,
            ["Backslash"] = 0x002b,
            ["KeyZ"] = 0x002c,
            ["KeyX"] = 0x002d,
            ["KeyC"] = 0x002e,
            ["KeyV"] = 0x002f,
            ["KeyB"] = 0x0030,
            ["KeyN"] = 0x0031,
            ["KeyM"] = 0x0032,
            ["Comma"] = 0x0033,
            ["Period"] = 0x0034,
            ["Slash"] = 0x0035,
            ["ShiftRight"] = 0x0036,
            ["NumpadMultiply"] = 0x0037,
            ["AltLeft"] = 0x0038,
            ["Space"] = 0x0039,
            ["CapsLock"] = 0x003a,
            ["F1"] = 0x003b,
            ["F2"] = 0x003c,
            ["F3"] = 0x003d,
            ["F4"] = 0x003e,
            ["F5"] = 0x003f,
            ["F6"] = 0x0040,
            ["F7"] = 0x0041,
            ["F8"] = 0x0042,
            ["F9"] = 0x0043,
            ["F10"] = 0x0044,
            ["Pause"] = 0x0045,
            ["ScrollLock"] = 0x0046,
            ["Numpad7"] = 0x0047,
            ["Numpad8"] = 0x0048,
            ["Numpad9"] = 0x0049,
            ["NumpadSubtract"] = 0x004a,
            ["Numpad4"] = 0x004b,
            ["Numpad5"] = 0x004c,
            ["Numpad6"] = 0x004d,
            ["NumpadAdd"] = 0x004e,
            ["Numpad1"] = 0x004f,
            ["Numpad2"] = 0x0050,
            ["Numpad3"] = 0x0051,
            ["Numpad0"] = 0x0052,
            ["NumpadDecimal"] = 0x0053,
            ["IntlBackslash"] = 0x0056,
            ["F11"] = 0x0057,
            ["F12"] = 0x0058,
            ["NumpadEqual"] = 0x0059,
            ["F13"] = 0x0064,
            ["F14"] = 0x0065,
            ["F15"] = 0x0066,
            ["F16"] = 0x0067,
            ["F17"] = 0x0068,
            ["F18"] = 0x0069,
            ["F19"] = 0x006a,
            ["F20"] = 0x006b,
            ["F21"] = 0x006c,
            ["F22"] = 0x006d,
            ["F23"] = 0x006e,
            ["KanaMode"] = 0x0070,
            ["Lang2"] = 0x0071,
            ["Lang1"] = 0x0072,
            ["IntlRo"] = 0x0073,
            ["F24"] = 0x0076,
            ["Lang4 "] = 0x0077,
            ["Lang3 "] = 0x0078,
            ["Convert"] = 0x0079,
            ["NonConvert"] = 0x007b,
            ["IntlYen"] = 0x007d,
            ["NumpadComma"] = 0x007e,
            ["Undo"] = 0xe008,
            ["Paste"] = 0xe00a,
            ["MediaTrackPrevious"] = 0xe010,
            ["Cut"] = 0xe017,
            ["Copy"] = 0xe018,
            ["MediaTrackNext"] = 0xe019,
            ["NumpadEnter"] = 0xe01c,
            ["ControlRight"] = 0xe01d,
            ["AudioVolumeMute"] = 0xe020,
            ["LaunchApp2"] = 0xe021,
            ["MediaPlayPause"] = 0xe022,
            ["MediaStop"] = 0xe024,
            ["Eject"] = 0xe02c,
            ["AudioVolumeDown"] = 0xe02e,
            ["AudioVolumeUp"] = 0xe030,
            ["BrowserHome"] = 0xe032,
            ["NumpadDivide"] = 0xe035,
            ["PrintScreen"] = 0xe037,
            ["AltRight"] = 0xe038,
            ["Help"] = 0xe03b,
            ["NumLock"] = 0xe045,
            ["Home"] = 0xe047,
            ["ArrowUp"] = 0xe048,
            ["PageUp"] = 0xe049,
            ["ArrowLeft"] = 0xe04b,
            ["ArrowRight"] = 0xe04d,
            ["End"] = 0xe04f,
            ["ArrowDown"] = 0xe050,
            ["PageDown"] = 0xe051,
            ["Insert"] = 0xe052,
            ["Delete"] = 0xe053,
            ["MetaLeft"] = 0xe05b,
            ["MetaRight"] = 0xe05c,
            ["ContextMenu"] = 0xe05d,
            ["Power"] = 0xe05e,
            ["Sleep"] = 0xe05f,
            ["WakeUp"] = 0xe063,
            ["BrowserSearch"] = 0xe065,
            ["BrowserFavorites"] = 0xe066,
            ["BrowserRefresh"] = 0xe067,
            ["BrowserStop"] = 0xe068,
            ["BrowserForward"] = 0xe069,
            ["BrowserBack"] = 0xe06a,
            ["LaunchApp1"] = 0xe06b,
            ["LaunchMail"] = 0xe06c,
            ["MediaSelect"] = 0xe06d,
        }.ToFrozenDictionary();
    }

    private uint GetTargetSessionId(int initialTarget, bool forceConsoleSession)
    {
        // If not force console, find target session.  If not present,
        // use last active session.
        var sessionId = PInvoke.WTSGetActiveConsoleSessionId();
        if (forceConsoleSession)
        {
            return sessionId;
        }

        var activeSessions = GetActiveSessions();
        if (activeSessions.Any(x => x.Id == initialTarget))
        {
            sessionId = (uint)initialTarget;
        }
        else
        {
            sessionId = activeSessions.Last().Id;
        }

        return sessionId;
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

        _logger.LogDebug("Invoking mouse wheel scroll. Point: {Point}, Delta: {Delta}, IsVertical: {IsVertical}.", normalizedPoint, delta, isVertical);

        var mouseInput = new MOUSEINPUT
        {
            dx = normalizedPoint.X,
            dy = normalizedPoint.Y,
            dwFlags = mouseEventFlags,
            mouseData = (uint)delta,
            dwExtraInfo = new nuint(extraInfo.Value.ToPointer()),
        };

        var input = new INPUT()
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

    private bool TryGetExplorerDuplicateToken(uint sessionId, [NotNullWhen(true)] out SafeFileHandle? primaryToken)
    {
        primaryToken = null;

        try
        {
            uint explorerPid = 0;
            var explorerProcs = Process.GetProcessesByName("explorer");
            foreach (var p in explorerProcs)
            {
                if ((uint)p.SessionId == sessionId)
                {
                    explorerPid = (uint)p.Id;
                    break;
                }
            }

            if (explorerPid == 0)
            {
                return false;
            }

            // Obtain a handle to the winlogon process;
            var explorerProcessHandle = PInvoke.OpenProcess(
                (PROCESS_ACCESS_RIGHTS)MAXIMUM_ALLOWED_RIGHTS,
                true,
                explorerPid);

            // Obtain a handle to the access token of the winlogon process.
            using var explorerSafeProcHandle = new SafeProcessHandle(explorerProcessHandle.Value, true);
            if (!PInvoke.OpenProcessToken(
                explorerSafeProcHandle,
                TOKEN_ACCESS_MASK.TOKEN_DUPLICATE,
                out var explorerToken))
            {
                PInvoke.CloseHandle(explorerProcessHandle);
                return false;
            }

            // Copy the access token of the winlogon process; the newly created token will be a primary token.
            var result = PInvoke.DuplicateTokenEx(
                explorerToken,
                (TOKEN_ACCESS_MASK)MAXIMUM_ALLOWED_RIGHTS,
                null,
                SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                TOKEN_TYPE.TokenPrimary,
                out primaryToken);

            PInvoke.CloseHandle(explorerProcessHandle);
            explorerToken.Close();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to duplicate Explorer token.  Last Win32 Error: {LastWin32Error}", Marshal.GetLastWin32Error());
            return false;
        }
    }

    private bool TryGetTokenInformation(SafeFileHandle token, TOKEN_INFORMATION_CLASS tokenClass, out nint infoPtr, out uint ptrSize)
    {
        _logger.LogInformation("Getting token information for class: {TokenInformationClass}", tokenClass);

        _ = PInvoke.GetTokenInformation(token, tokenClass, null, 0, out var neededLength);
        _logger.LogInformation("Token information length needed: {NeededLength}.", neededLength);

        infoPtr = Marshal.AllocHGlobal((int)neededLength);
        if (!PInvoke.GetTokenInformation(token, tokenClass, infoPtr.ToPointer(), neededLength, out ptrSize))
        {
            _logger.LogWarning("Failed to get token information.  Last Win32 Error: {LastWin32Error}", Marshal.GetLastWin32Error());
            return false;
        }
        _logger.LogInformation("Successfully got token information.");
        return true;
    }
    private bool TryGetUserPrimaryToken(uint sessionId, [NotNullWhen(true)] out SafeFileHandle? primaryToken)
    {
        HANDLE userToken = default;
        primaryToken = null;

        if (!PInvoke.WTSQueryUserToken(sessionId, ref userToken))
        {
            _logger.LogWarning("Failed to query user token.");
            return false;
        }
        using var safeUserToken = new SafeAccessTokenHandle(userToken);

        if (!PInvoke.DuplicateTokenEx(
            safeUserToken,
            0,
            null,
            SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
            TOKEN_TYPE.TokenPrimary,
            out primaryToken))
        {
            _logger.LogWarning("Failed to duplicate primary token.");
            return false;
        }

        return true;
    }
    [StructLayout(LayoutKind.Explicit)]
    private struct ShortHelper(short value)
    {
        [FieldOffset(0)]
        public short Value = value;
        [FieldOffset(0)]
        public byte Low;
        [FieldOffset(1)]
        public byte High;
    }

}