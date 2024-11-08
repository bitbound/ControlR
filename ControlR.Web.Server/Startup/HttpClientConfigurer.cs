namespace ControlR.Web.Server.Startup;

public static class HttpClientConfigurer
{
  public static void ConfigureHttpClient(IServiceProvider services, HttpClient client)
  {
    var options = services.GetRequiredService<IOptionsMonitor<AppOptions>>();
    client.BaseAddress = options.CurrentValue.ServerBaseUri ??
                         throw new InvalidOperationException("ServerBaseUri cannot be empty.");
  }
}