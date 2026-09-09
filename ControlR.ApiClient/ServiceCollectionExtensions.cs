using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
      return new ControlrApiClientAuthState(options.PersonalAccessToken, options.ServiceAccountApiKey);
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
    services.TryAddTransient(sp => sp.GetRequiredService<IControlrApi>().Internal);
    services.TryAddTransient(sp => sp.GetRequiredService<IControlrApi>().V1);

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
      return new ControlrApiClientAuthState(options.PersonalAccessToken, options.ServiceAccountApiKey);
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
    services.TryAddTransient(sp => sp.GetRequiredService<IControlrApi>().Internal);
    services.TryAddTransient(sp => sp.GetRequiredService<IControlrApi>().V1);

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

  /// <summary>
  /// <para>
  ///   Adds the <see cref="IControlrApiClientFactory"/> service for applications that integrate with
  ///   multiple, runtime-discovered ControlR servers.
  /// </para>
  /// <para>
  ///   Unlike <see cref="AddControlrApiClient(IServiceCollection, Action{ControlrApiClientOptions})"/>,
  ///   which configures a single server statically, the factory creates one self-contained client per
  ///   named target via <see cref="IControlrApiClientFactory.GetOrCreateClient"/>.
  /// </para>
  /// <para>
  ///   This registration is server-only. Do not use it from Blazor WebAssembly; use
  ///   <see cref="AddControlrApiClient(IServiceCollection, Action{ControlrApiClientOptions})"/> there instead.
  /// </para>
  /// </summary>
  /// <param name="services">
  ///   The <see cref="IServiceCollection"/> to which the services are added.
  /// </param>
  /// <param name="configureFactoryOptions">
  ///   An optional action used to configure the <see cref="ControlrApiClientFactoryOptions"/>.
  /// </param>
  /// <returns>
  ///   The <see cref="IServiceCollection"/> to allow for chaining further calls.
  /// </returns>
  public static IServiceCollection AddControlrApiClientFactory(
    this IServiceCollection services,
    Action<ControlrApiClientFactoryOptions>? configureFactoryOptions = null)
  {
    services.TryAddSingleton(TimeProvider.System);

    var optionsBuilder = services
      .AddOptions<ControlrApiClientFactoryOptions>()
      .Validate(options => options.SweeperInterval > TimeSpan.Zero,
        $"{nameof(ControlrApiClientFactoryOptions.SweeperInterval)} must be greater than zero.")
      .Validate(
        options => options.MaxIdleClientLifetime is null || options.MaxIdleClientLifetime > TimeSpan.Zero,
        $"{nameof(ControlrApiClientFactoryOptions.MaxIdleClientLifetime)} must be greater than zero when set.")
      .Validate(
        options => options.MaxTrackedClients is null || options.MaxTrackedClients > 0,
        $"{nameof(ControlrApiClientFactoryOptions.MaxTrackedClients)} must be greater than zero when set. Leave it null for no limit.")
      .ValidateOnStart();

    if (configureFactoryOptions is not null)
    {
      optionsBuilder.Configure(configureFactoryOptions);
    }

    services.TryAddSingleton<IControlrApiClientFactory>(sp => new ControlrApiClientFactory(
      sp.GetRequiredService<IOptions<ControlrApiClientFactoryOptions>>().Value,
      sp.GetRequiredService<TimeProvider>(),
      sp.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance));

    return services;
  }

  /// <summary>
  /// <para>
  ///   Adds the <see cref="IControlrApiClientFactory"/> service, loading factory configuration from the
  ///   specified configuration section.
  /// </para>
  /// <para>
  ///   Per-server options are still supplied at client-creation time via
  ///   <see cref="IControlrApiClientFactory.GetOrCreateClient"/>.
  /// </para>
  /// </summary>
  /// <param name="services">
  ///   The <see cref="IServiceCollection"/> to which the services are added.
  /// </param>
  /// <param name="configuration">
  ///   The <see cref="IConfiguration"/> instance to bind factory options from.
  /// </param>
  /// <param name="configurationSectionName">
  ///   The name of the configuration section containing the <see cref="ControlrApiClientFactoryOptions"/>.
  /// </param>
  /// <returns>
  ///   The <see cref="IServiceCollection"/> to allow for chaining further calls.
  /// </returns>
  public static IServiceCollection AddControlrApiClientFactory(
    this IServiceCollection services,
    IConfiguration configuration,
    string configurationSectionName)
  {
    services.TryAddSingleton(TimeProvider.System);

    services
      .AddOptions<ControlrApiClientFactoryOptions>()
      .Bind(configuration.GetSection(configurationSectionName))
      .Validate(options => options.SweeperInterval > TimeSpan.Zero,
        $"{nameof(ControlrApiClientFactoryOptions.SweeperInterval)} must be greater than zero.")
      .Validate(
        options => options.MaxIdleClientLifetime is null || options.MaxIdleClientLifetime > TimeSpan.Zero,
        $"{nameof(ControlrApiClientFactoryOptions.MaxIdleClientLifetime)} must be greater than zero when set.")
      .Validate(
        options => options.MaxTrackedClients is null || options.MaxTrackedClients > 0,
        $"{nameof(ControlrApiClientFactoryOptions.MaxTrackedClients)} must be greater than zero when set. Leave it null for no limit.")
      .ValidateOnStart();

    services.TryAddSingleton<IControlrApiClientFactory>(sp => new ControlrApiClientFactory(
      sp.GetRequiredService<IOptions<ControlrApiClientFactoryOptions>>().Value,
      sp.GetRequiredService<TimeProvider>(),
      sp.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance));

    return services;
  }

  /// <summary>
  /// <para>
  ///   Adds the <see cref="IControlrApiClientFactory"/> service, loading factory configuration from the
  ///   specified configuration section using the builder's <see cref="IHostApplicationBuilder.Configuration"/>.
  /// </para>
  /// </summary>
  /// <param name="builder">
  ///   The <see cref="IHostApplicationBuilder"/> to add the services to.
  /// </param>
  /// <param name="configurationSectionName">
  ///   The name of the configuration section containing the <see cref="ControlrApiClientFactoryOptions"/>.
  /// </param>
  /// <returns>
  ///   The <see cref="IHostApplicationBuilder"/> to allow for chaining further calls.
  /// </returns>
  public static IHostApplicationBuilder AddControlrApiClientFactory(
    this IHostApplicationBuilder builder,
    string configurationSectionName)
  {
    builder.Services.AddControlrApiClientFactory(builder.Configuration, configurationSectionName);
    return builder;
  }
}