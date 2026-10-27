using ControlR.Assist.Services;

namespace ControlR.Assist;

internal static class StaticServiceProvider
{
  private static ServiceProvider? _designTimeProvider;
  private static ServiceProvider? _provider;

  public static IServiceProvider Instance => _provider ?? GetDesignTimeProvider();

  public static void Build()
  {
    if (_provider is not null)
    {
      return;
    }

    _provider = CreateServices().BuildServiceProvider();
  }

  private static IServiceCollection CreateServices()
  {
    var services = new ServiceCollection();
    services.AddControlrAssist();
    return services;
  }

  private static ServiceProvider GetDesignTimeProvider() =>
    _designTimeProvider ??= CreateServices().BuildServiceProvider();
}
