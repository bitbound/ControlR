// Define the undocumented function signature
// wmsgapi.dll is usually found in System32
using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Windows;

public static class Wmsgapi
{
  [DllImport("wmsgapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
  public static extern int WmsgSendMessage(
      int sessionId,
      int msg,
      int wParam,
      IntPtr lParam
  );
}
