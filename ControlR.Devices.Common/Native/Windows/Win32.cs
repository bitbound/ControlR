using ControlR.Shared.Dtos.SidecarDtos;
using ControlR.Shared.Models;
using Microsoft.Win32.SafeHandles;
using System.Collections.Frozen;
using System.Diagnostics;
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

namespace ControlR.Devices.Common.Native.Windows;

[SupportedOSPlatform("windows6.0.6000")]
public static unsafe partial class Win32
{
    private const uint MAXIMUM_ALLOWED_RIGHTS = 0x2000000;
    private static FrozenDictionary<string, int>? _keyMap;
    private static HDESK _lastInputDesktop;
    public static bool CreateInteractiveSystemProcess(
        string commandLine,
        int targetSessionId,
        bool forceConsoleSession,
        string desktopName,
        bool hiddenWindow,
        out Process? startedProcess)
    {
        startedProcess = null;
        uint winlogonPid = 0;

        // If not force console, find target session.  If not present,
        // use last active session.
        var dwSessionId = PInvoke.WTSGetActiveConsoleSessionId();
        if (!forceConsoleSession)
        {
            var activeSessions = GetActiveSessions();
            if (activeSessions.Any(x => x.Id == targetSessionId))
            {
                dwSessionId = (uint)targetSessionId;
            }
            else
            {
                dwSessionId = activeSessions.Last().Id;
            }
        }

        // Obtain the process ID of the winlogon process that is running within the currently active session.
        var processes = Process.GetProcessesByName("winlogon");
        foreach (var p in processes)
        {
            if ((uint)p.SessionId == dwSessionId)
            {
                winlogonPid = (uint)p.Id;
            }
        }

        // Obtain a handle to the winlogon process.
        var winLogonHandle = PInvoke.OpenProcess(
            (PROCESS_ACCESS_RIGHTS)MAXIMUM_ALLOWED_RIGHTS,
            false,
            winlogonPid);

        using var winLogonSafeHandle = new SafeProcessHandle(winLogonHandle.Value, true);

        // Obtain a handle to the access token of the winlogon process.

        if (!PInvoke.OpenProcessToken(winLogonSafeHandle, TOKEN_ACCESS_MASK.TOKEN_DUPLICATE, out var winLogonAccessToken))
        {
            PInvoke.CloseHandle(winLogonHandle);
            return false;
        }

        // Security attibute structure used in DuplicateTokenEx and CreateProcessAsUser.
        var securityAttributes = new SECURITY_ATTRIBUTES();
        securityAttributes.nLength = (uint)Marshal.SizeOf(securityAttributes);

        // Copy the access token of the winlogon process; the newly created token will be a primary token.
        if (!PInvoke.DuplicateTokenEx(
            winLogonAccessToken,
            (TOKEN_ACCESS_MASK)MAXIMUM_ALLOWED_RIGHTS,
            securityAttributes,
            SECURITY_IMPERSONATION_LEVEL.SecurityIdentification,
            TOKEN_TYPE.TokenPrimary,
            out var duplicatedToken))
        {
            PInvoke.CloseHandle(winLogonHandle);
            winLogonAccessToken.Close();
            return false;
        }

        // By default, CreateProcessAsUser creates a process on a non-interactive window station, meaning
        // the window station has a desktop that is invisible and the process is incapable of receiving
        // user input. To remedy this we set the lpDesktop parameter to indicate we want to enable user
        // interaction with the new process.
        var startupInfo = new STARTUPINFOW();
        startupInfo.cb = (uint)Marshal.SizeOf(startupInfo);
        var desktopPtr = Marshal.StringToHGlobalAuto($"winsta0\\{desktopName}\0");
        startupInfo.lpDesktop = new PWSTR((char*)desktopPtr.ToPointer());

        // Flags that specify the priority and creation method of the process.
        PROCESS_CREATION_FLAGS dwCreationFlags;

        if (hiddenWindow)
        {
            dwCreationFlags = PROCESS_CREATION_FLAGS.NORMAL_PRIORITY_CLASS | PROCESS_CREATION_FLAGS.CREATE_NO_WINDOW | PROCESS_CREATION_FLAGS.DETACHED_PROCESS;
            startupInfo.dwFlags = STARTUPINFOW_FLAGS.STARTF_USESHOWWINDOW;
            startupInfo.wShowWindow = 0;
        }
        else
        {
            dwCreationFlags = PROCESS_CREATION_FLAGS.NORMAL_PRIORITY_CLASS | PROCESS_CREATION_FLAGS.CREATE_NEW_CONSOLE;
        }

        var cmdLineSpan = $"{commandLine}\0".ToCharArray().AsSpan();
        // Create a new process in the current user's logon session.
        var result = PInvoke.CreateProcessAsUser(
            duplicatedToken,
            null,
            ref cmdLineSpan,
            securityAttributes,
            securityAttributes,
            false,
            dwCreationFlags,
            IntPtr.Zero.ToPointer(),
            null,
            in startupInfo,
            out var procInfo);

        // Invalidate the handles.
        PInvoke.CloseHandle(winLogonHandle);
        Marshal.FreeHGlobal(desktopPtr);
        winLogonAccessToken.Close();
        duplicatedToken.Close();

        if (result)
        {
            try
            {
                startedProcess = Process.GetProcessById((int)procInfo.dwProcessId);
            }
            catch
            {
                return false;
            }
        }

        return result;
    }



    public static IEnumerable<WindowsSession> GetActiveSessions()
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
        var dataSize = Marshal.SizeOf(typeof(WtsApi32.WTS_SESSION_INFO));
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

    public static IEnumerable<WindowsSession> GetActiveSessionsCsWin32()
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
            
            var dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFOW));

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

    public static bool GetCurrentThreadDesktop(out string desktopName)
    {
        var threadId = PInvoke.GetCurrentThreadId();
        return GetThreadDesktop(threadId, out desktopName);
    }

    public static bool GetInputDesktop(out string desktopName)
    {
        var inputDesktop = GetInputDesktop();
        if (inputDesktop.IsNull)
        {
            desktopName = string.Empty;
            return false;
        }

        return GetDesktopName(inputDesktop, out desktopName);
    }
    public static bool GetThreadDesktop(uint threadId, out string desktopName)
    {
        var hdesk = PInvoke.GetThreadDesktop(threadId);
        if (hdesk.IsNull)
        {
            desktopName = string.Empty;
            return false;
        }

        return GetDesktopName(hdesk, out desktopName);
    }

    public static string GetUsernameFromSessionId(uint sessionId)
    {
        var result = PInvoke.WTSQuerySessionInformation(HANDLE.Null, sessionId, WTS_INFO_CLASS.WTSUserName, out var username, out var bytesReturned);

        if (result && bytesReturned > 1)
        {
            return username.ToString();
        }

        return string.Empty;
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [SupportedOSPlatform("windows6.1")]
    public static void InvokeCtrlAltDel()
    {
        var isService = Process.GetCurrentProcess().SessionId == 0;
        PInvoke.SendSAS(!isService);
    }

    public static void InvokeMouseButtonEvent(int x, int y, int button, bool isPressed)
    {
        var extraInfo = PInvoke.GetMessageExtraInfo();
        var mouseEventFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE | MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK;

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
            default:
                return;
        }

        var normalizedPoint = GetNormalizedPoint(x, y);

        var mouseInput = new MOUSEINPUT
        {
            dx = normalizedPoint.X,
            dy = normalizedPoint.Y,
            dwFlags = mouseEventFlags,
            mouseData = 0,
            dwExtraInfo = (nuint)extraInfo.Value,
        };

        var input = new INPUT()
        {
            type = INPUT_TYPE.INPUT_MOUSE,
            Anonymous = { mi = mouseInput }
        };

        var inputs = new INPUT[] { input };

        PInvoke.SendInput(inputs, Marshal.SizeOf<INPUT>());
    }

    public static void MovePointer(int x, int y, MovePointerType moveType)
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
            dwExtraInfo = (nuint)extraInfo.Value,
        };

        var input = new INPUT()
        {
            type = INPUT_TYPE.INPUT_MOUSE,
            Anonymous = { mi = mouseInput }
        };

        var inputs = new INPUT[] { input };

        PInvoke.SendInput(inputs, Marshal.SizeOf<INPUT>());
    }

    public static nint OpenInputDesktop()
    {
        return PInvoke.OpenInputDesktop(0, true, (DESKTOP_ACCESS_FLAGS)0x10000000u).Value;
    }

    public static bool SwitchToInputDesktop()
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

    private static FrozenDictionary<string, int> GetKeyMap()
    {
        return _keyMap ??= new Dictionary<string, int>()
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

    private static Point GetNormalizedPoint(int x, int y)
    {
        var width = (double)PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
        var height = (double)PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);

        return new Point((int)(x / width * 65535), (int)(y / height * 65535));
    }
}