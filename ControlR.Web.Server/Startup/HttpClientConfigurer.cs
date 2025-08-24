namespace ControlR.Web.Server.Startup;

public static class HttpClientConfigurer
{
  public static void ConfigureHttpClient(IServiceProvider services, HttpClient client)
  {
    var contextAccessor = services.GetRequiredService<IHttpContextAccessor>();
    var context = contextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext cannot be null.");

    client.BaseAddress = context.Request.ToOrigin();

    if (context.User.Identity?.IsAuthenticated == true)
    {
        var cookies = context.Request.Headers.Cookie.ToString();
        if (!string.IsNullOrEmpty(cookies))
        {
          client.DefaultRequestHeaders.Add("Cookie", cookies);
        }
    }
  }
}