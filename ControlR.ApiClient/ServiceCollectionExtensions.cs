using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace ControlR.ApiClient;

public static class ServiceCollectionExtensions
{
  /// <summary>
  /// <para>
  ///   Adds services for interacting with the ControlR API via the generated Kiota client.
  /// </para>
  /// <para>
  ///   The <see cref="IControlrApiClientFactory"/> will be registered as a singleton service,
  ///   which can be used to create instances of <see cref="ControlrApiClient"/>.
  /// </para>
  /// <para>
  ///   The <see cref="ControlrApiClient"/> will be registered as a transient service and can also be injected directly.
  /// </para>
  /// </summary>
  /// <param name="services">
  ///   The <see cref="IServiceCollection"/> to which the services are added.
  /// </param>
  /// <param name="configureOptions">
  ///   The action used to configure the <see cref="ControlrApiClientOptions"/>.
  /// </param>
  /// <returns>
  ///   The <see cref="IServiceCollection"/> to allow for chaining further calls.
  /// </returns>
  public static IServiceCollection AddControlrApiClient(
    this IServiceCollection services,
    Action<ControlrApiClientOptions> configureOptions)
  {
    // Register and validate options using the options pattern
    services
      .AddOptions<ControlrApiClientOptions>()
      .Configure(configureOptions)
      .Validate(options => options.BaseUrl is not null, "BaseUrl is required.")
      .Validate(options => !string.IsNullOrWhiteSpace(options.PersonalAccessToken), "PersonalAccessToken is required.")
      .ValidateOnStart();

    // Add Kiota handlers to the dependency injection container
    services.AddKiotaHandlers();

    // Register the factory for the ControlR API client
    services
      .AddHttpClient<IControlrApiClientFactory, ControlrApiClientFactory>(
      (sp, client) =>
      {
        var options = sp.GetRequiredService<IOptionsMonitor<ControlrApiClientOptions>>().CurrentValue;
        client.BaseAddress = options.BaseUrl;
        client.DefaultRequestHeaders.Add("x-personal-token", options.PersonalAccessToken);
      })
      .AttachKiotaHandlers();

    services.AddTransient(sp => sp.GetRequiredService<IControlrApiClientFactory>().GetClient());

    return services;
  }

  /// <summary>
  /// <para>
  ///   Adds services for interacting with the ControlR API via the generated Kiota client.
  /// </para>
  /// <para>
  ///   Configuration is loaded from the specified configuration section.
  /// </para>
  /// <para>
  ///   <see cref="IControlrApiClientFactory"/> will be registered as a singleton service,
  ///   which can be used to create instances of <see cref="ControlrApiClient"/>.
  /// </para>
  /// <para>
  ///   The <see cref="ControlrApiClient"/> will be registered as a transient service and can also be injected directly.
  /// </para>
  /// </summary>
  /// <param name="services">
  ///   The <see cref="IServiceCollection"/> to which the services are added.
  /// </param>
  /// <param name="configuration">
  ///   The <see cref="IConfiguration"/> instance to bind options from.
  /// </param>
  /// <param name="configurationSectionName">
  ///   The name of the configuration section containing the <see cref="ControlrApiClientOptions"/>.
  /// </param>
  /// <returns>
  ///   The <see cref="IServiceCollection"/> to allow for chaining further calls.
  /// </returns>
  public static IServiceCollection AddControlrApiClient(
    this IServiceCollection services,
    IConfiguration configuration,
    string configurationSectionName)
  {
    // Register and validate options using the options pattern
    services
      .AddOptions<ControlrApiClientOptions>()
      .Bind(configuration.GetSection(configurationSectionName))
      .Validate(options => options.BaseUrl is not null, "BaseUrl is required.")
      .Validate(options => !string.IsNullOrWhiteSpace(options.PersonalAccessToken), "PersonalAccessToken is required.")
      .ValidateOnStart();

    // Add Kiota handlers to the dependency injection container
    services.AddKiotaHandlers();

    // Register the factory for the ControlR API client
    services
      .AddHttpClient<IControlrApiClientFactory, ControlrApiClientFactory>(
      (sp, client) =>
      {
        var options = sp.GetRequiredService<IOptionsMonitor<ControlrApiClientOptions>>().CurrentValue;
        client.BaseAddress = options.BaseUrl;
        client.DefaultRequestHeaders.Add("x-personal-token", options.PersonalAccessToken);
      })
      .AttachKiotaHandlers();

    services.AddTransient(sp => sp.GetRequiredService<IControlrApiClientFactory>().GetClient());
    return services;
  }

  /// <summary>
  /// <para>
  ///   Adds services for interacting with the ControlR API via the generated Kiota client.
  /// </para>
  /// <para>
  ///   Configuration is loaded from the specified configuration section using the builder's <see cref="IHostApplicationBuilder.Configuration"/>.
  /// </para>
  /// <para>
  ///   The <see cref="IControlrApiClientFactory"/> will be registered as a singleton service,
  ///   which can be used to create instances of <see cref="ControlrApiClient"/>.
  /// </para>
  /// <para>
  ///   The <see cref="ControlrApiClient"/> will be registered as a transient service and can also be injected directly.
  /// </para>
  /// </summary>
  /// <param name="builder">
  ///   The <see cref="IHostApplicationBuilder"/> to add the services to.
  /// </param>
  /// <param name="configurationSectionName">
  ///   The name of the configuration section containing the <see cref="ControlrApiClientOptions"/>.
  /// </param>
  /// <returns>
  ///   The <see cref="IHostApplicationBuilder"/> to allow for chaining further calls.
  /// </returns>
  public static IHostApplicationBuilder AddControlrApiClient(
    this IHostApplicationBuilder builder,
    string configurationSectionName)
  {
    builder.Services.AddControlrApiClient(builder.Configuration, configurationSectionName);
    return builder;
  }

  /// <summary>
  /// Adds the Kiota handlers to the service collection.
  /// </summary>
  /// <param name="services"><see cref="IServiceCollection"/> to add the services to</param>
  /// <returns><see cref="IServiceCollection"/> as per convention</returns>
  /// <remarks>The handlers are added to the http client by the <see cref="AttachKiotaHandlers(IHttpClientBuilder)"/> call, which requires them to be pre-registered in DI</remarks>
  private static IServiceCollection AddKiotaHandlers(this IServiceCollection services)
  {
    // Dynamically load the Kiota handlers from the Client Factory
    var kiotaHandlers = KiotaClientFactory.GetDefaultHandlerActivatableTypes();
    // And register them in the DI container
    foreach (var handler in kiotaHandlers)
    {
      services.AddTransient(handler);
    }

    return services;
  }

  /// <summary>
  /// Adds the Kiota handlers to the http client builder.
  /// </summary>
  /// <param name="builder"></param>
  /// <returns></returns>
  /// <remarks>
  /// Requires the handlers to be registered in DI by <see cref="AddKiotaHandlers(IServiceCollection)"/>.
  /// The order in which the handlers are added is important, as it defines the order in which they will be executed.
  /// </remarks>
  private static IHttpClientBuilder AttachKiotaHandlers(this IHttpClientBuilder builder)
  {
    // Dynamically load the Kiota handlers from the Client Factory
    var kiotaHandlers = KiotaClientFactory.GetDefaultHandlerActivatableTypes();
    // And attach them to the http client builder
    foreach (var handler in kiotaHandlers)
    {
      builder.AddHttpMessageHandler((sp) => (DelegatingHandler)sp.GetRequiredService(handler));
    }

    return builder;
  }
}