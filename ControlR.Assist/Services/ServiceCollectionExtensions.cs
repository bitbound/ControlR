using ControlR.Assist.ViewModels;
using ControlR.Assist.Views;
using Microsoft.Extensions.Logging;

namespace ControlR.Assist.Services;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddControlrAssist(this IServiceCollection services)
  {
    // Views and view models are registered with plain lambdas rather than the open-generic
    // overloads, which build an expression tree the linker has to guess at.
    services.AddSingleton<HomeViewModel>();
    services.AddSingleton<MainWindowViewModel>();
    services.AddSingleton<MainWindow>(provider => new MainWindow(provider.GetRequiredService<MainWindowViewModel>()));

    services.AddLogging();
    services.AddSingleton<IAssistSettingsService, AssistSettingsService>();
    services.AddSingleton<IAssistSessionHostFactory, AssistSessionHostFactory>();
    services.AddSingleton<IAssistSignalRClient, AssistHubClient>();
    services.AddSingleton<AssistSessionController>(provider =>
    {
      var controller = new AssistSessionController(
        provider.GetRequiredService<IAssistSignalRClient>(),
        provider.GetRequiredService<IAssistSessionHostFactory>(),
        provider.GetRequiredService<IAssistSettingsService>(),
        provider.GetRequiredService<ILogger<AssistSessionController>>());

      var hubClient = provider.GetRequiredService<IAssistSignalRClient>();
      hubClient.OnConsentRequested = controller.HandleConsentRequested;
      hubClient.OnSessionEnded = controller.HandleSessionEnded;
      hubClient.OnSessionStarted = controller.HandleSessionStarted;
      hubClient.OnReconnected = controller.HandleReconnected;

      return controller;
    });

    return services;
  }
}
