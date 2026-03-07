using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

[assembly: InternalsVisibleTo("ControlR.DesktopClient.Tests")]

#if IS_WINDOWS
[assembly: SupportedOSPlatform("windows8.0")]
#elif IS_MACOS
[assembly: SupportedOSPlatform("macos")]
#elif IS_LINUX
[assembly: SupportedOSPlatform("linux")]
#endif