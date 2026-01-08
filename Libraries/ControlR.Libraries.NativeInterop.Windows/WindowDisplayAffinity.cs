using System.Runtime.Versioning;

namespace ControlR.Libraries.NativeInterop.Windows;

[SupportedOSPlatform("windows6.1")]
public enum WindowDisplayAffinity : uint
{
  None = 0x00000000,
  Monitor = 0x00000001,
  ExcludeFromCapture = 0x00000011
}
