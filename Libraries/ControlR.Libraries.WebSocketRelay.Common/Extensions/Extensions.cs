using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.WebSockets;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Libraries.WebSocketRelay.Common.Extensions;
public static class Extensions
{
  public static IServiceCollection AddWebSocketRelay(
      this IServiceCollection services)
  {
    services.AddWebSockets(_ => { });
    services.AddSingleton<ISessionStore, SessionStore>();
    return services;
  }

  public static IApplicationBuilder MapWebSocketRelay(
      this IApplicationBuilder app,
      string websocketPath = "/relay")
  {
    app.UseWhen(x => x.Request.Path.StartsWithSegments(websocketPath), x =>
    {
      x.UseWebSockets();
      x.UseMiddleware<WebSocketRelayMiddleware>();
    });
    return app;
  }
}
