global using Microsoft.Extensions.DependencyInjection;

#if WINDOWS_BUILD
global using ControlR.DesktopClient.Windows;
global using ControlR.DesktopClient.Windows.Services;
#elif MAC_BUILD
global using ControlR.DesktopClient.Mac;
global using ControlR.DesktopClient.Mac.Services;
global using ControlR.Libraries.NativeInterop.Unix.MacOs;
#elif LINUX_BUILD
global using ControlR.DesktopClient.Linux;
#endif

#if UNIX_BUILD
global using ControlR.Libraries.NativeInterop.Unix;
#endif