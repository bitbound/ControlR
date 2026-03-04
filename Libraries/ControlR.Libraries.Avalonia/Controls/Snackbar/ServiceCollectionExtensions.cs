using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlR.Libraries.Avalonia.Controls.Snackbar;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddControlrSnackbar(this IServiceCollection services)
  {
    return services.AddControlrSnackbar(_ => { });
  }

  public static IServiceCollection AddControlrSnackbar(
    this IServiceCollection services,
    Action<SnackbarOptions> configureOptions)
  {
    services.AddOptions();
    services.Configure(configureOptions);
    services.TryAddSingleton<ISnackbar, SnackbarService>();
    return services;
  }
}