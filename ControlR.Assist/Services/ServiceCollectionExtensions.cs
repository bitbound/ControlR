using ControlR.Assist.ViewModels;
using ControlR.Assist.Views;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Api.Contracts.Hubs;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Libraries.Avalonia.Services;
using ControlR.Libraries.Serilog;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Locks;
using ControlR.Libraries.Shared.Services.Processes;
using ControlR.Libraries.Signalr.Client;
using ControlR.Libraries.Signalr.Client.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ControlR.Assist.Services;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddControlrAssist(this IServiceCollection services)
  {
    // Views and view models are registered with plain lambdas rather than the open-generic
    // overloads, which build an expression tree the linker has to guess at.
    services.AddSingleton<AboutViewModel>();
    services.AddSingleton<HomeViewModel>();
    services.AddSingleton<SettingsViewModel>();
    services.AddSingleton<MainWindowViewModel>();
    services.AddSingleton<MainWindow>(provider => new MainWindow(provider.GetRequiredService<MainWindowViewModel>()));

    // BootstrapSerilog supplies the sink but not the ILogger<T> registration, so the app container
    // needs AddLogging too. Omitting it leaves every injected logger unresolvable at runtime.
    services.AddLogging();

    // Same sink the desktop client and the agent use, so a support session produces a rolling daily
    // file next to the settings the app already writes.
    var configuration = new ConfigurationBuilder()
      .AddEnvironmentVariables()
      .Build();

    services.BootstrapSerilog(
      configuration,
      AssistSettingsService.LogFilePath,
      TimeSpan.FromDays(7),
      config =>
      {
        if (SystemEnvironment.Instance.IsDebug)
        {
          config.MinimumLevel.Debug();
        }
      });

    services.AddSingleton<IAssistSettingsService, AssistSettingsService>();
    services.AddSingleton<IThemeProvider, ThemeProvider>();
    services.AddSingleton<IAssistSessionHostFactory, AssistSessionHostFactory>();

    // The session host cannot build its platform services without these, and they live in the app
    // container for every app that starts one.
    services.AddSingleton<IUiDispatcher, UiDispatcher>();
    services.AddSingleton<IAsyncLock, KeyedLock>();
    services.AddSingleton<IProcessManager, ProcessManager>();
    services.AddStronglyTypedSignalrClient<IAssistHub, IAssistHubClient, AssistHubClient>(ServiceLifetime.Singleton);
    services.AddSingleton<INavigationService, NavigationService>();
    services.AddSingleton<IUrlLauncher, UrlLauncher>();
    services.AddSingleton(provider =>
    {
      var coordinator = new AssistSessionCoordinator(
        provider.GetRequiredService<IHubConnection<IAssistHub>>(),
        provider.GetRequiredService<IAssistSessionHostFactory>(),
        provider.GetRequiredService<IAssistSettingsService>(),
        provider.GetRequiredService<ILogger<AssistSessionCoordinator>>());

      var connection = provider.GetRequiredService<IHubConnection<IAssistHub>>();
      connection.Closed += _ => coordinator.HandleConnectionLost();
      connection.Reconnected += _ => coordinator.HandleReconnected();

      return coordinator;
    });

    return services;
  }
}
