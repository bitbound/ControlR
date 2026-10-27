using ControlR.Assist.ViewModels;
using ControlR.Assist.Views;

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

    return services;
  }
}
