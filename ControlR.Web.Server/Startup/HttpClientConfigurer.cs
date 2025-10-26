namespace ControlR.Web.Server.Startup;

public static class HttpClientConfigurer
{
  public static void ConfigureHttpClient(IServiceProvider services, HttpClient client)
  {
    var contextAccessor = services.GetRequiredService<IHttpContextAccessor>();
    var context = contextAccessor.HttpContext;

    if (context != null)
    {
      client.BaseAddress = context.Request.ToOrigin();
    }
  }
}
