global using Microsoft.Extensions.DependencyInjection;
global using ControlR.DesktopClient.Common.ServiceInterfaces;
global using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;

#if WINDOWS_BUILD
global using ControlR.DesktopClient.Windows;
global using ControlR.DesktopClient.Windows.Services;
global using ControlR.Libraries.NativeInterop.Windows;
#elif MAC_BUILD
global using ControlR.DesktopClient.Mac;
global using ControlR.DesktopClient.Mac.Services;
global using ControlR.Libraries.NativeInterop.Unix.MacOs;
#elif LINUX_BUILD
global using ControlR.DesktopClient.Linux;
global using ControlR.DesktopClient.Linux.Services;
#endif

#if UNIX_BUILD
global using ControlR.Libraries.NativeInterop.Unix;
#endif