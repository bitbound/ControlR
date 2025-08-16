using System.Runtime.Versioning;

#if WINDOWS_BUILD
[assembly: SupportedOSPlatform("windows8.0")]
#elif MAC_BUILD
[assembly: SupportedOSPlatform("macos")]
#elif LINUX_BUILD
[assembly: SupportedOSPlatform("linux")]
#endif