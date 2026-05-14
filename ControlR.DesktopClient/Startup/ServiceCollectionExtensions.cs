using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
using ControlR.DesktopClient.Services;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Hosting;

namespace ControlR.DesktopClient.Startup;

internal static class ServiceCollectionExtensions
{
  public static IServiceCollection AddDesktopShellServices(
    this IServiceCollection services,
    string? instanceId)
  {
    services.AddOptions();

    services.Configure<DesktopClientOptions>(options =>
    {
      options.InstanceId = instanceId;
    });

    services
      .AddControlrIpcClient<DesktopClientRpcService>()
      .AddSingleton(TimeProvider.System)
      .AddSingleton<IMessenger>(new WeakReferenceMessenger())
      .AddSingleton<IProcessManager, ProcessManager>()
      .AddSingleton<IFileSystem, FileSystem>()
      .AddSingleton<IFileAccessPermissions, FileAccessPermissions>()
      .AddSingleton<ISystemEnvironment, SystemEnvironment>()
      .AddSingleton<INavigationProvider, NavigationProvider>()
      .AddSingleton<IMainWindowProvider, MainWindowProvider>()
      .AddSingleton<IThemeProvider, ThemeProvider>()
      .AddSingleton<IAppViewModel, AppViewModel>()
      .AddSingleton<IMainWindowViewModel, MainWindowViewModel>()
      .AddSingleton<IConnectionsViewModel, ConnectionsViewModel>()
      .AddSingleton<ISettingsViewModel, SettingsViewModel>()
      .AddSingleton<IAboutViewModel, AboutViewModel>()
      .AddSingleton<IRemoteControlHostManager, RemoteControlHostManager>()
      .AddSingleton<IDialogProvider, DialogProvider>()
      .AddSingleton<IUserInteractionService, UserInteractionService>()

      .AddSingleton<IDesktopPreviewProvider, DesktopPreviewProvider>()
      .AddSingleton<IChatSessionManager, ChatSessionManager>()
      .AddSingleton<IpcClientManager>()
      .AddSingleton<IIpcClientAccessor>(sp => sp.GetRequiredService<IpcClientManager>())
      .AddSingleton<IToaster, Toaster>()
      .AddSingleton<IUiThread, UiThread>()
      .AddSingleton<IImageUtility, ImageUtility>()
      .AddSingleton<DesktopApplicationLifetime>()
      .AddSingleton<IHostApplicationLifetime>(sp => sp.GetRequiredService<DesktopApplicationLifetime>())
      .AddSingleton<IViewModelFactory, ViewModelFactory>()
      .AddSingleton<IUrlLauncher, UrlLauncher>()
      .AddSingleton<IWaiter, Waiter>()
      .AddSingleton<INavigationItemProvider, ShellNavigationItemProvider>()
      .AddTransient<MainWindow>()
      .AddTransient<ConnectionsView>()
      .AddTransient<SettingsView>()
      .AddTransient<AboutView>()
      .AddTransient<IMessageBoxViewModel, MessageBoxViewModel>()
      .AddTransient<ChatWindow>()
      .AddTransient<IChatWindowViewModel, ChatWindowViewModel>()
      .AddTransient<ToastWindow>()
      .AddTransient<IToastWindowViewModel, ToastWindowViewModel>()
      .AddHostedService(sp => sp.GetRequiredService<IpcClientManager>())
      .AddHostedService(sp => sp.GetRequiredService<DesktopApplicationLifetime>());

    return services;
  }
}
