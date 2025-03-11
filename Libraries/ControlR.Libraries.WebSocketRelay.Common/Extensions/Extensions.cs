using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.WebSockets;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Libraries.WebSocketRelay.Common.Extensions;
public static class Extensions
{
    public static IServiceCollection AddWebSocketBridge(
        this IServiceCollection services)
    {
        services.AddWebSockets(_ => { });
        services.AddSingleton<ISessionStore, SessionStore>();
        return services;
    }

    public static IApplicationBuilder MapWebSocketBridge(
        this IApplicationBuilder app, 
        string websocketPath = "/bridge")
    {
        app.UseWhen(x => x.Request.Path.StartsWithSegments(websocketPath), x =>
        {
            x.UseWebSockets();
            x.UseMiddleware<WebSocketBridgeMiddleware>();
        });
        return app;
    }
}
