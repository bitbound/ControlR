using Microsoft.Extensions.DependencyInjection;

namespace ControlR.ApiClient;

public static class ControlrApiClientBuilder
{
  private static readonly Lock _servicesLock = new();

  private static IControlrApiClientFactory? _clientFactory;
  private static IServiceCollection? _serviceCollection;
  private static IServiceProvider? _serviceProvider;

  /// <summary>
  ///   Creates a new instance of <see cref="ControlrApiClient"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  ///   Internally, the <see cref="IHttpClientFactory"/> is used to manage the lifetime of the underlying <see cref="HttpClient"/> instances,
  ///   its message handlers, and associated socket resources.  As such, you don't have to worry about socket exhaustion or other common pitfalls.
  /// </para>
  /// <para>
  ///   See <see href="https://learn.microsoft.com/en-us/openapi/kiota/tutorials/dotnet-dependency-injection"/> for more information.
  /// </para>
  /// </remarks>
  /// <exception cref="InvalidOperationException"></exception>
  public static ControlrApiClient GetClient()
  {
    if (_clientFactory is null)
    {
      throw new InvalidOperationException(
        $"The API client builder has not been initialized.  Call {nameof(ControlrApiClientBuilder.Initialize)} first.");
    }

    return _clientFactory.GetClient();
  }

  public static void Initialize(Action<ControlrApiClientOptions> configureOptions)
  {
    using var lockScope = _servicesLock.EnterScope();
    if (_clientFactory is not null)
    {
      return;
    }
    _serviceCollection ??= new ServiceCollection();
    _serviceCollection.AddControlrApiClient(configureOptions);
    _serviceProvider = _serviceCollection.BuildServiceProvider();
    _clientFactory = _serviceProvider.GetRequiredService<IControlrApiClientFactory>();
  }
}
