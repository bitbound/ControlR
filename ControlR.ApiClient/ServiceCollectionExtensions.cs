using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace ControlR.ApiClient;

public static class ServiceCollectionExtensions
{
  /// <summary>
  /// <para>
  ///   Adds services for interacting with the ControlR API via the custom HTTP API client.
  /// </para>
  /// <para>
  ///   The <see cref="IControlrApiClientFactory"/> will be registered as a singleton service,
  ///   which can be used to create instances of <see cref="IControlrApi"/>.
  /// </para>
  /// <para>
  ///   The <see cref="IControlrApi"/> will be registered as a transient service and can also be injected directly.
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
      .ValidateOnStart();

    // Register the factory for the ControlR API client.
    services
      .AddHttpClient<IControlrApi, ControlrApi>(
      (sp, client) =>
      {
        var options = sp.GetRequiredService<IOptionsMonitor<ControlrApiClientOptions>>().CurrentValue;
        client.BaseAddress = options.BaseUrl;
        ApplyAuthHeader(client, options);
      });
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
  ///   <see cref="IControlrApiClientFactory"/> will be registered as a singleton service,
  ///   which can be used to create instances of <see cref="IControlrApi"/>.
  /// </para>
  /// <para>
  ///   The <see cref="IControlrApi"/> will be registered as a transient service and can also be injected directly.
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
    // Register and validate options using the options pattern.
    services
      .AddOptions<ControlrApiClientOptions>()
      .Bind(configuration.GetSection(configurationSectionName))
      .Validate(options => options.BaseUrl is not null, "BaseUrl is required.")
      .ValidateOnStart();

    // Register the factory for the ControlR API client.
    services
      .AddHttpClient<IControlrApi, ControlrApi>(
      (sp, client) =>
      {
        var options = sp.GetRequiredService<IOptionsMonitor<ControlrApiClientOptions>>().CurrentValue;
        client.BaseAddress = options.BaseUrl;
        ApplyAuthHeader(client, options);
      });

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
  ///   The <see cref="IControlrApiClientFactory"/> will be registered as a singleton service,
  ///   which can be used to create instances of <see cref="IControlrApi"/>.
  /// </para>
  /// <para>
  ///   The <see cref="IControlrApi"/> will be registered as a transient service and can also be injected directly.
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

  private static void ApplyAuthHeader(HttpClient client, ControlrApiClientOptions options)
  {
    client.DefaultRequestHeaders.Remove(ControlrApiClientOptions.PersonalAccessTokenHeader);
    client.DefaultRequestHeaders.Remove(ControlrApiClientAuthOptions.AuthorizationHeader);
    client.DefaultRequestHeaders.Authorization = null;

    if (!options.Auth.TryGetAuthHeader(out var headerName, out var headerValue))
    {
      return;
    }

    if (headerName == ControlrApiClientAuthOptions.AuthorizationHeader)
    {
      client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(headerValue);
      return;
    }

    client.DefaultRequestHeaders.Add(headerName, headerValue);
  }
}