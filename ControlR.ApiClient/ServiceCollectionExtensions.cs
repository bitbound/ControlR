using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ControlR.ApiClient.Interfaces.Internal;
using ControlR.ApiClient.Interfaces.V1;

namespace ControlR.ApiClient;

public static class ServiceCollectionExtensions
{
  /// <summary>
  /// <para>
  ///   Adds services for interacting with the ControlR API via the custom HTTP API client.
  /// </para>
  /// <para>
  ///   The <see cref="IControlrApi"/> will be registered as a transient service and can be injected directly.
  ///   It provides <see cref="IControlrInternalApi"/> and <see cref="IControlrV1Api"/>
  ///   via the <c>Internal</c> and <c>V1</c> properties.
  /// </para>
  /// <para>
  ///   The sub-interfaces are also registered individually for callers that prefer narrower injection.
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
    services.TryAddSingleton(TimeProvider.System);

    // Register and validate options using the options pattern
    services
      .AddOptions<ControlrApiClientOptions>()
      .Configure(configureOptions)
      .Validate(options => options.BaseUrl is not null, "BaseUrl is required.")
      .ValidateOnStart();

    services.TryAddSingleton(sp =>
    {
      var options = sp.GetRequiredService<IOptionsMonitor<ControlrApiClientOptions>>().CurrentValue;
      return new ControlrApiClientAuthState(options.PersonalAccessToken);
    });

    services.TryAddSingleton<IBearerTokenRefresher, BearerTokenRefresher>();
    services.TryAddTransient<ControlrApiAuthHeaderHandler>();

    // Register the typed HttpClient for ControlrApi.
    services.AddHttpClient(
      ControlrApiClientNames.UnauthenticatedClient,
      (sp, client) =>
      {
        var options = sp.GetRequiredService<IOptionsMonitor<ControlrApiClientOptions>>().CurrentValue;
        client.BaseAddress = options.BaseUrl;
      });

    services
      .AddHttpClient<ControlrApi>(
      (sp, client) =>
      {
        var options = sp.GetRequiredService<IOptionsMonitor<ControlrApiClientOptions>>().CurrentValue;
        client.BaseAddress = options.BaseUrl;
      })
      .AddHttpMessageHandler<ControlrApiAuthHeaderHandler>();
    services.TryAddTransient<IControlrApi>(sp => sp.GetRequiredService<ControlrApi>());
    services.TryAddTransient<IControlrInternalApi>(sp => sp.GetRequiredService<IControlrApi>().Internal);
    services.TryAddTransient<IControlrV1Api>(sp => sp.GetRequiredService<IControlrApi>().V1);

    services.TryAddSingleton<IControlrAuthSession, ControlrAuthSession>();
    return services;
  }

  /// <summary>
  /// <para>
  ///   Adds services for interacting with the ControlR API via the custom HTTP API client.
  /// </para>
  /// <para>
  ///   Configuration is loaded from the specified configuration section.
  /// </para>
  /// <para>
  ///   The <see cref="IControlrApi"/> will be registered as a transient service and can be injected directly.
  ///   It provides <see cref="IControlrInternalApi"/> and <see cref="IControlrV1Api"/>
  ///   via the <c>Internal</c> and <c>V1</c> properties.
  /// </para>
  /// <para>
  ///   The sub-interfaces are also registered individually for callers that prefer narrower injection.
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
    services.TryAddSingleton(TimeProvider.System);

    // Register and validate options using the options pattern.
    services
      .AddOptions<ControlrApiClientOptions>()
      .Bind(configuration.GetSection(configurationSectionName))
      .Validate(options => options.BaseUrl is not null, "BaseUrl is required.")
      .ValidateOnStart();

    services.TryAddSingleton(sp =>
    {
      var options = sp.GetRequiredService<IOptionsMonitor<ControlrApiClientOptions>>().CurrentValue;
      return new ControlrApiClientAuthState(options.PersonalAccessToken);
    });

    services.TryAddSingleton<IBearerTokenRefresher, BearerTokenRefresher>();
    services.TryAddTransient<ControlrApiAuthHeaderHandler>();

    // Register the typed HttpClient for ControlrApi.
    services.AddHttpClient(
      ControlrApiClientNames.UnauthenticatedClient,
      (sp, client) =>
      {
        var options = sp.GetRequiredService<IOptionsMonitor<ControlrApiClientOptions>>().CurrentValue;
        client.BaseAddress = options.BaseUrl;
      });

    services
      .AddHttpClient<ControlrApi>(
      (sp, client) =>
      {
        var options = sp.GetRequiredService<IOptionsMonitor<ControlrApiClientOptions>>().CurrentValue;
        client.BaseAddress = options.BaseUrl;
      })
      .AddHttpMessageHandler<ControlrApiAuthHeaderHandler>();
    services.TryAddTransient<IControlrApi>(sp => sp.GetRequiredService<ControlrApi>());
    services.TryAddTransient<IControlrInternalApi>(sp => sp.GetRequiredService<IControlrApi>().Internal);
    services.TryAddTransient<IControlrV1Api>(sp => sp.GetRequiredService<IControlrApi>().V1);

    services.TryAddSingleton<IControlrAuthSession, ControlrAuthSession>();

    return services;
  }

  /// <summary>
  /// <para>
  ///   Adds services for interacting with the ControlR API via the custom HTTP API client.
  /// </para>
  /// <para>
  ///   Configuration is loaded from the specified configuration section using the builder's <see cref="IHostApplicationBuilder.Configuration"/>.
  /// </para>
  /// <para>
  ///   The <see cref="IControlrApi"/> will be registered as a transient service and can be injected directly.
  ///   It provides <see cref="IControlrInternalApi"/> and <see cref="IControlrV1Api"/>
  ///   via the <c>Internal</c> and <c>V1</c> properties.
  /// </para>
  /// <para>
  ///   The sub-interfaces are also registered individually for callers that prefer narrower injection.
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
}