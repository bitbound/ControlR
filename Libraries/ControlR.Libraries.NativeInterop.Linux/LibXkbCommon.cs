using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Linux;

public static class LibXkbCommon
{
  private const string LibraryName = "libxkbcommon.so.0";

  [DllImport(LibraryName)]
  public static extern uint xkb_keysym_from_name(string name, uint flags);
}
