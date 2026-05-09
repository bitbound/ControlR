using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

[assembly: InternalsVisibleTo("ControlR.DesktopClient.Tests")]

#if IS_WINDOWS
[assembly: SupportedOSPlatform("windows")]
#endif
#if IS_MACOS
[assembly: SupportedOSPlatform("macos")]
#endif
#if IS_LINUX
[assembly: SupportedOSPlatform("linux")]
#endif