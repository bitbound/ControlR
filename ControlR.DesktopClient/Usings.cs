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
global using ControlR.Libraries.Avalonia;
global using ControlR.Libraries.Avalonia.Theming;
global using ControlR.Libraries.Avalonia.Controls;
global using System.Diagnostics.CodeAnalysis;
global using CommunityToolkit.Mvvm.ComponentModel;
global using ControlR.DesktopClient.Common;
global using ControlR.Libraries.Api.Contracts.Enums;
global using ControlR.DesktopClient.Common.ViewModelInterfaces;
global using ControlR.Libraries.Shared.Collections;

#if IS_WINDOWS
global using ControlR.DesktopClient.Windows;
global using ControlR.DesktopClient.Windows.Services;
global using ControlR.Libraries.NativeInterop.Windows;
#endif

#if IS_MACOS
global using ControlR.DesktopClient.Mac;
global using ControlR.DesktopClient.Mac.Services;
global using ControlR.DesktopClient.ViewModels.Mac;
global using ControlR.DesktopClient.Views.Mac;
global using ControlR.Libraries.NativeInterop.Unix;
global using ControlR.Libraries.NativeInterop.Mac;
global using ControlR.DesktopClient.Mac.Helpers;
global using ControlR.DesktopClient.Services.Mac;
#endif

#if IS_LINUX
global using ControlR.DesktopClient.Linux;
global using ControlR.DesktopClient.Linux.Services;
global using ControlR.DesktopClient.Views.Linux;
global using ControlR.Libraries.NativeInterop.Linux;
global using ControlR.Libraries.NativeInterop.Unix;
global using ControlR.DesktopClient.ViewModels.Linux;
global using ControlR.DesktopClient.Linux.XdgPortal;
global using ControlR.DesktopClient.Services.Linux;
#endif