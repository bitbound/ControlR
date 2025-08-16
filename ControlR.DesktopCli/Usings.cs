#if WINDOWS_BUILD
global using ControlR.DesktopClient.Windows;
#elif MAC_BUILD
global using ControlR.DesktopClient.Mac;
#elif LINUX_BUILD
global using ControlR.DesktopClient.Linux;
#endif