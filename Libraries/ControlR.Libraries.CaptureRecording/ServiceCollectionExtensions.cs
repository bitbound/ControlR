using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlR.Libraries.CaptureRecording;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddControlrCaptureRecording(
    this IServiceCollection services,
    Action<CaptureRecorderOptions>? configureRecorder = null,
    Action<CapturePlayerOptions>? configurePlayer = null)
  {
    if (configureRecorder is not null)
    {
      services.Configure(configureRecorder);
    }

    if (configurePlayer is not null)
    {
      services.Configure(configurePlayer);
    }

    services.TryAddSingleton(TimeProvider.System);
    services.TryAddSingleton<ICaptureRecorderFactory, CaptureRecorderFactory>();
    services.TryAddSingleton<ICapturePlayerFactory, CapturePlayerFactory>();

    return services;
  }
}
