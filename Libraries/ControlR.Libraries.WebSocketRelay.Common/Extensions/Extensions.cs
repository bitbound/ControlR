using ControlR.Libraries.WebSocketRelay.Common.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ControlR.Libraries.WebSocketRelay.Common.Extensions;
public static class Extensions
{
  public static IServiceCollection AddWebSocketRelay(
    this IServiceCollection services)
  {
    return services.AddWebSocketRelay(_ => { });
  }

  public static IServiceCollection AddWebSocketRelay(
    this IServiceCollection services,
    Action<WebSocketRelayOptions> configureOptions)
  {
    services.AddWebSockets(_ => { });
    services.TryAddSingleton<IRelaySessionStore, RelaySessionStore>();
    services.AddOptions();
    services.Configure(configureOptions);
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