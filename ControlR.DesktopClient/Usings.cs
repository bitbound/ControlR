global using Microsoft.Extensions.DependencyInjection;
global using ControlR.DesktopClient.Common.ServiceInterfaces;
global using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
global using Avalonia.Threading;
global using ControlR.DesktopClient.Extensions;
global using ControlR.DesktopClient.ViewModels;
global using ControlR.DesktopClient.ViewModels.Internals;
global using ControlR.Libraries.Shared.Extensions;
global using ControlR.DesktopClient.Views;
global using CommunityToolkit.Mvvm.Input;
global using ControlR.DesktopClient.Controls;
global using ControlR.Libraries.Avalonia.Theming;
global using System.Diagnostics.CodeAnalysis;
global using CommunityToolkit.Mvvm.ComponentModel;
global using ControlR.DesktopClient.Common;
global using ControlR.DesktopClient.Common.ViewModelInterfaces;
global using ControlR.Libraries.Shared.Collections;

#if IS_WINDOWS
global using ControlR.DesktopClient.Windows;
global using ControlR.Libraries.NativeInterop.Windows;
#endif

#if IS_MACOS
global using ControlR.DesktopClient.Mac;
#endif

#if IS_LINUX
global using ControlR.DesktopClient.Linux;
#endif