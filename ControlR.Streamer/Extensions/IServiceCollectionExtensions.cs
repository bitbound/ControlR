using ControlR.Streamer.Helpers;
using ControlR.Streamer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Streamer.Extensions;

public static class ServiceCollectionExtensions
{
  /// <summary>
  ///   Adds the following services with the specified lifetimes:
  ///   <list type="bullet">
  ///     <item>
  ///       <see cref="IScreenGrabber" /> as Singleton
  ///     </item>
  ///     <item>
  ///       <see cref="IBitmapUtility" /> as Singleton
  ///     </item>
  ///   </list>
  /// </summary>
  /// <param name="services"></param>
  /// <returns></returns>
  public static IServiceCollection AddScreenCapturer(this IServiceCollection services)
  {
    return services
      .AddSingleton<IBitmapUtility, BitmapUtility>()
      .AddSingleton<IScreenGrabber, ScreenGrabber>()
      .AddSingleton<IDxOutputGenerator, DxOutputGenerator>();
  }
}