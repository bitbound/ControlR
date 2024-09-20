using ControlR.Libraries.ScreenCapture.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Libraries.ScreenCapture.Extensions;

public static class ServiceCollectionExtensions
{
  /// <summary>
  ///   Adds the following services with the specified lifetimes:
  ///   <list type="bullet">
  ///     <item>
  ///       <see cref="IScreenCapturer" /> as Singleton
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
      .AddSingleton<IScreenCapturer, ScreenCapturer>()
      .AddSingleton<IDxOutputGenerator, DxOutputGenerator>();
  }
}