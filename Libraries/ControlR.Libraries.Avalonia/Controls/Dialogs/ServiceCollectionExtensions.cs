using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Libraries.Avalonia.Controls.Dialogs;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddControlrDialogs(this IServiceCollection services)
  {
    services.TryAddSingleton<IDialogProvider, DialogProvider>();
    return services;
  }
}
