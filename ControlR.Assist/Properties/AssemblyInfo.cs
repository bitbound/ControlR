using System.Runtime.Versioning;

#if IS_WINDOWS
[assembly: SupportedOSPlatform("windows")]
#endif
#if IS_MACOS
[assembly: SupportedOSPlatform("macos")]
#endif
#if IS_LINUX
[assembly: SupportedOSPlatform("linux")]
#endif