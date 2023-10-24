using ControlR.Shared.Models;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using WTS_CONNECTSTATE_CLASS = global::Windows.Win32.System.RemoteDesktop.WTS_CONNECTSTATE_CLASS;
using WTS_INFO_CLASS = global::Windows.Win32.System.RemoteDesktop.WTS_INFO_CLASS;
using WTS_SESSION_INFOW = global::Windows.Win32.System.RemoteDesktop.WTS_SESSION_INFOW;

namespace ControlR.Devices.Common.Native.Windows;

[SupportedOSPlatform("windows6.0.6000")]
public static partial class Win32
{
    public static IEnumerable<WindowsSession> GetActiveSessions()
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
                return sessions.ToImmutableList();
            }

            var dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFOW));

            for (var i = 0; i < count; i += dataSize)
            {
                var current = ppSessionInfos + i;
                var sessionInfo = *current;

                if (sessionInfo.State == WTS_CONNECTSTATE_CLASS.WTSActive && sessionInfo.SessionId != consoleSessionId)
                {
                    sessions.Add(new WindowsSession()
                    {
                        Id = sessionInfo.SessionId,
                        Name = sessionInfo.pWinStationName.ToString(),
                        Type = WindowsSessionType.RDP,
                        Username = GetUsernameFromSessionId(sessionInfo.SessionId)
                    });
                }
            }
        }

        return sessions.ToImmutableList();
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
}