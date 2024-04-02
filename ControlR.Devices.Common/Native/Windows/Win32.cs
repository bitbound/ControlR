using ControlR.Shared.Models;
using Microsoft.Win32.SafeHandles;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.StationsAndDesktops;
using Windows.Win32.System.Threading;
using WTS_CONNECTSTATE_CLASS = global::Windows.Win32.System.RemoteDesktop.WTS_CONNECTSTATE_CLASS;
using WTS_INFO_CLASS = global::Windows.Win32.System.RemoteDesktop.WTS_INFO_CLASS;
using WTS_SESSION_INFOW = global::Windows.Win32.System.RemoteDesktop.WTS_SESSION_INFOW;

namespace ControlR.Devices.Common.Native.Windows;

[SupportedOSPlatform("windows6.0.6000")]
public static unsafe partial class Win32
{
    private const uint MAXIMUM_ALLOWED_RIGHTS = 0x2000000;
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
        Process[] processes = Process.GetProcessesByName("winlogon");
        foreach (Process p in processes)
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

        if (!PInvoke.OpenProcessToken(winLogonSafeHandle, TOKEN_ACCESS_MASK.TOKEN_DUPLICATE, out var hPToken))
        {
            PInvoke.CloseHandle(winLogonHandle);
            return false;
        }

        // Security attibute structure used in DuplicateTokenEx and CreateProcessAsUser.
        var sa = new SECURITY_ATTRIBUTES();
        sa.nLength = (uint)Marshal.SizeOf(sa);

        // Copy the access token of the winlogon process; the newly created token will be a primary token.
        if (!PInvoke.DuplicateTokenEx(
            hPToken,
            (TOKEN_ACCESS_MASK)MAXIMUM_ALLOWED_RIGHTS,
            sa,
            SECURITY_IMPERSONATION_LEVEL.SecurityIdentification,
            TOKEN_TYPE.TokenPrimary,
            out var hUserTokenDup))
        {
            PInvoke.CloseHandle(winLogonHandle);
            hPToken.Close();
            return false;
        }

        // By default, CreateProcessAsUser creates a process on a non-interactive window station, meaning
        // the window station has a desktop that is invisible and the process is incapable of receiving
        // user input. To remedy this we set the lpDesktop parameter to indicate we want to enable user
        // interaction with the new process.
        var si = new STARTUPINFOW();
        si.cb = (uint)Marshal.SizeOf(si);
        var desktopPtr = Marshal.StringToHGlobalAuto($"winsta0\\{desktopName}\0");
        si.lpDesktop = new PWSTR((char*)desktopPtr.ToPointer());

        // Flags that specify the priority and creation method of the process.
        PROCESS_CREATION_FLAGS dwCreationFlags;

        if (hiddenWindow)
        {
            dwCreationFlags = PROCESS_CREATION_FLAGS.NORMAL_PRIORITY_CLASS | PROCESS_CREATION_FLAGS.CREATE_NO_WINDOW | PROCESS_CREATION_FLAGS.DETACHED_PROCESS;
            si.dwFlags = STARTUPINFOW_FLAGS.STARTF_USESHOWWINDOW;
            si.wShowWindow = 0;
        }
        else
        {
            dwCreationFlags = PROCESS_CREATION_FLAGS.NORMAL_PRIORITY_CLASS | PROCESS_CREATION_FLAGS.CREATE_NEW_CONSOLE;
        }

        var cmdLineSpan = $"{commandLine}\0".ToCharArray().AsSpan();
        // Create a new process in the current user's logon session.
        var result = PInvoke.CreateProcessAsUser(
            hUserTokenDup,
            null,
            ref cmdLineSpan,
            sa,
            sa,
            false,
            dwCreationFlags,
            IntPtr.Zero.ToPointer(),
            null,
            in si,
            out var procInfo);

        // Invalidate the handles.
        PInvoke.CloseHandle(winLogonHandle);
        Marshal.FreeHGlobal(desktopPtr);
        hPToken.Close();
        hUserTokenDup.Close();

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
}