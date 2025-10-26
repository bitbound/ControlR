using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Windows;

public static class WtsApi32
{
  public static nint WtsCurrentServerHandle { get; } = nint.Zero;
  
  [DllImport("wtsapi32.dll", SetLastError = true)]
  public static extern int WTSEnumerateSessions(
    nint hServer,
    int reserved,
    int version,
    ref nint ppSessionInfo,
    ref int pCount);

  [DllImport("wtsapi32.dll", SetLastError = false)]
  public static extern void WTSFreeMemory(nint memory);

  [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
  public static extern nint WTSOpenServer(string pServerName);

  [DllImport("Wtsapi32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static extern bool WTSQuerySessionInformation(nint hServer, uint sessionId, WtsInfoClass wtsInfoClass,
    out nint ppBuffer, out uint pBytesReturned);

  public enum WtsConnectstateClass
  {
    WtsActive,
    WtsConnected,
    WtsConnectQuery,
    WtsShadow,
    WtsDisconnected,
    WtsIdle,
    WtsListen,
    WtsReset,
    WtsDown,
    WtsInit
  }

  public enum WtsInfoClass
  {
    WtsInitialProgram,
    WtsApplicationName,
    WtsWorkingDirectory,
    WtsoemId,
    WtsSessionId,
    WtsUserName,
    WtsWinStationName,
    WtsDomainName,
    WtsConnectState,
    WtsClientBuildNumber,
    WtsClientName,
    WtsClientDirectory,
    WtsClientProductId,
    WtsClientHardwareId,
    WtsClientAddress,
    WtsClientDisplay,
    WtsClientProtocolType,
    WtsIdleTime,
    WtsLogonTime,
    WtsIncomingBytes,
    WtsOutgoingBytes,
    WtsIncomingFrames,
    WtsOutgoingFrames,
    WtsClientInfo,
    WtsSessionInfo
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct WtsSessionInfo
  {
    public uint SessionID;
    [MarshalAs(UnmanagedType.LPStr)] public string pWinStationName;
    public WtsConnectstateClass State;
  }
}