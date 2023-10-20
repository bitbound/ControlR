using ControlR.Shared.Models;
using PInvoke;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using static PInvoke.Kernel32;
using Win32PInvoke = global::Windows.Win32.PInvoke;

namespace ControlR.Devices.Common.Native.Windows;

public static partial class Win32
{
    private const int TOKEN_DUPLICATE = 0x0002;
    private static User32.SafeDesktopHandle? _lastInputDesktop;

    private enum WTS_INFO_CLASS
    {
        WTSInitialProgram,
        WTSApplicationName,
        WTSWorkingDirectory,
        WTSOEMId,
        WTSSessionId,
        WTSUserName,
        WTSWinStationName,
        WTSDomainName,
        WTSConnectState,
        WTSClientBuildNumber,
        WTSClientName,
        WTSClientDirectory,
        WTSClientProductId,
        WTSClientHardwareId,
        WTSClientAddress,
        WTSClientDisplay,
        WTSClientProtocolType,
        WTSIdleTime,
        WTSLogonTime,
        WTSIncomingBytes,
        WTSOutgoingBytes,
        WTSIncomingFrames,
        WTSOutgoingFrames,
        WTSClientInfo,
        WTSSessionInfo
    }

    public static bool CreateInteractiveSystemProcess(
        string commandLine,
        int targetSessionId,
        bool forceConsoleSession,
        string desktopName,
        bool hiddenWindow,
        out PROCESS_INFORMATION procInfo)
    {
        int winlogonPid = 0;
        var hProcess = IntPtr.Zero;

        procInfo = new PROCESS_INFORMATION();

        // If not force console, find target session.  If not present,
        // use last active session.
        var dwSessionId = WTSGetActiveConsoleSessionId();
        if (!forceConsoleSession)
        {
            var activeSessions = GetActiveSessions();
            if (activeSessions.Any(x => x.Id == targetSessionId))
            {
                dwSessionId = (uint)targetSessionId;
            }
            else
            {
                dwSessionId = (uint)activeSessions.Last().Id;
            }
        }

        // Obtain the process ID of the winlogon process that is running within the currently active session.
        Process[] processes = Process.GetProcessesByName("winlogon");
        foreach (Process p in processes)
        {
            if ((uint)p.SessionId == dwSessionId)
            {
                winlogonPid = p.Id;
            }
        }

        var maximumAllowedMask = new ACCESS_MASK((uint)ACCESS_MASK.SpecialRight.MAXIMUM_ALLOWED);

        // Obtain a handle to the winlogon process.
        var safeProcess = OpenProcess(maximumAllowedMask, false, winlogonPid);
        hProcess = safeProcess.DangerousGetHandle();

        // Obtain a handle to the access token of the winlogon process.
        if (!AdvApi32.OpenProcessToken(hProcess, new ACCESS_MASK(TOKEN_DUPLICATE), out var hPToken))
        {
            CloseHandle(hProcess);
            return false;
        }

        // Security attibute structure used in DuplicateTokenEx and CreateProcessAsUser.
        var sa = new SECURITY_ATTRIBUTES();
        sa.nLength = Marshal.SizeOf(sa);

        // Copy the access token of the winlogon process; the newly created token will be a primary token.
        if (!AdvApi32.DuplicateTokenEx(
            hPToken,
            maximumAllowedMask,
            sa,
            SECURITY_IMPERSONATION_LEVEL.SecurityIdentification,
            AdvApi32.TOKEN_TYPE.TokenPrimary,
            out var hUserTokenDup))
        {
            CloseHandle(hProcess);
            hPToken.Close();
            return false;
        }

        // By default, CreateProcessAsUser creates a process on a non-interactive window station, meaning
        // the window station has a desktop that is invisible and the process is incapable of receiving
        // user input. To remedy this we set the lpDesktop parameter to indicate we want to enable user
        // interaction with the new process.
        var si = new STARTUPINFO();
        si.cb = Marshal.SizeOf(si);
        si.lpDesktop_IntPtr = Marshal.StringToHGlobalAuto(@$"winsta0\{desktopName}");

        // Flags that specify the priority and creation method of the process.
        CreateProcessFlags dwCreationFlags;
        if (hiddenWindow)
        {
            dwCreationFlags = CreateProcessFlags.NORMAL_PRIORITY_CLASS | CreateProcessFlags.CREATE_NO_WINDOW | CreateProcessFlags.DETACHED_PROCESS;
            si.dwFlags = StartupInfoFlags.STARTF_USESHOWWINDOW;
            si.wShowWindow = 0;
        }
        else
        {
            dwCreationFlags = CreateProcessFlags.NORMAL_PRIORITY_CLASS | CreateProcessFlags.CREATE_NEW_CONSOLE;
        }

        // Create a new process in the current user's logon session.
        var result = CreateProcessAsUser(
            hUserTokenDup.DangerousGetHandle(),
            null,
            commandLine,
            sa,
            sa,
            false,
            dwCreationFlags,
            IntPtr.Zero,
            null,
            ref si,
            out procInfo);

        // Invalidate the handles.
        CloseHandle(hProcess);
        hPToken.Close();
        hUserTokenDup.Close();

        return result;
    }

    public static List<WindowsSession> GetActiveSessions()
    {
        var sessions = new List<WindowsSession>();
        var consoleSessionId = WTSGetActiveConsoleSessionId();
        sessions.Add(new WindowsSession()
        {
            Id = (int)consoleSessionId,
            Type = SessionType.Console,
            Name = "Console",
            Username = GetUsernameFromSessionId((int)consoleSessionId)
        });

        var enumSessionResult = WtsApi32.WTSEnumerateSessions(
            WtsApi32.WTS_CURRENT_SERVER_HANDLE,
            Reserved: 0,
            Version: 1,
            out IntPtr ppSessionInfos,
            out var count);

        if (!enumSessionResult)
        {
            return sessions;
        }

        var dataSize = Marshal.SizeOf(typeof(WtsApi32.WTS_SESSION_INFO));
        var current = ppSessionInfos;

        if (enumSessionResult)
        {
            for (int i = 0; i < count; i++)
            {
                var sessionInfo = Marshal.PtrToStructure<WtsApi32.WTS_SESSION_INFO>(current);
                current += dataSize;

                if (sessionInfo.State == WtsApi32.WTS_CONNECTSTATE_CLASS.WTSActive && sessionInfo.SessionID != consoleSessionId)
                {
                    sessions.Add(new WindowsSession()
                    {
                        Id = sessionInfo.SessionID,
                        Name = sessionInfo.WinStationName,
                        Type = SessionType.RDP,
                        Username = GetUsernameFromSessionId(sessionInfo.SessionID)
                    });
                }
            }
        }

        return sessions;
    }

    public static string GetCommandLineString()
    {
        var commandLinePtr = GetCommandLine();
        return Marshal.PtrToStringAuto(commandLinePtr) ?? string.Empty;
    }

    public static bool GetCurrentThreadDesktop(out string desktopName)
    {
        var threadId = GetCurrentThreadId();
        return GetThreadDesktop((uint)threadId, out desktopName);
    }

    public static bool GetInputDesktop(out string desktopName)
    {
        using var inputDesktop = OpenInputDesktop();
        if (inputDesktop.IsInvalid)
        {
            desktopName = string.Empty;
            return false;
        }

        return GetDesktopName(inputDesktop, out desktopName);
    }

    public static bool GetThreadDesktop(uint threadId, out string desktopName)
    {
        using var inputDesktop = User32.GetThreadDesktop(threadId);
        if (inputDesktop.IsInvalid)
        {
            desktopName = string.Empty;
            return false;
        }

        return GetDesktopName(inputDesktop, out desktopName);
    }

    public static string GetUsernameFromSessionId(int sessionId)
    {
        var username = string.Empty;

        if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, WTS_INFO_CLASS.WTSUserName, out var buffer, out var strLen) && strLen > 1)
        {
            username = Marshal.PtrToStringAnsi(buffer);
            WtsApi32.WTSFreeMemory(buffer);
        }

        return username ?? string.Empty;
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [SupportedOSPlatform("windows6.1")]
    public static void InvokeCtrlAltDel()
    {
        var isService = Process.GetCurrentProcess().SessionId == 0;
        Win32PInvoke.SendSAS(!isService);
    }

    public static User32.SafeDesktopHandle OpenInputDesktop()
    {
        return User32.OpenInputDesktop(User32.DesktopCreationFlags.None, true, ACCESS_MASK.GenericRight.GENERIC_ALL);
    }

    public static User32.MessageBoxResult ShowMessageBox(IntPtr owner,
        string message,
        string caption,
        User32.MessageBoxOptions options)
    {
        return User32.MessageBox(owner, message, caption, options);
    }

    public static bool SwitchToInputDesktop()
    {
        try
        {
            _lastInputDesktop?.Close();
            var inputDesktop = OpenInputDesktop();
            if (inputDesktop.IsInvalid)
            {
                return false;
            }

            var result = User32.SetThreadDesktop(inputDesktop) && User32.SwitchDesktop(inputDesktop);
            _lastInputDesktop = inputDesktop;
            return result;
        }
        catch
        {
            return false;
        }
    }

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetCommandLine();

    private static bool GetDesktopName(User32.SafeDesktopHandle handle, out string desktopName)
    {
        var outValue = Marshal.AllocHGlobal(256);
        var outLength = Marshal.AllocHGlobal(256);
        var desktopHandle = handle.DangerousGetHandle();

        if (!User32.GetUserObjectInformation(desktopHandle, User32.ObjectInformationType.UOI_NAME, outValue, 256, outLength))
        {
            desktopName = string.Empty;
            return false;
        }

        desktopName = Marshal.PtrToStringAuto(outValue)?.Trim() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(desktopName);
    }

    [LibraryImport("advapi32", SetLastError = true), SuppressUnmanagedCodeSecurity]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenProcessToken(IntPtr processHandle, int desiredAccess, ref IntPtr tokenHandle);

    [DllImport("Wtsapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSQuerySessionInformation(IntPtr hServer, int sessionId, WTS_INFO_CLASS wtsInfoClass, out IntPtr ppBuffer, out uint pBytesReturned);
}