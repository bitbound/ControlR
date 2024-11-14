#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
#pragma warning disable CA1401 // P/Invokes should not be visible
using System.Runtime.InteropServices;

namespace ControlR.Libraries.DevicesNative.Linux;

public static class Libc
{
  [DllImport("libc", EntryPoint = "geteuid", SetLastError = true)]
  public static extern uint Geteuid();


  [DllImport("libc", EntryPoint = "setsid", SetLastError = true)]
  public static extern int Setsid();
}