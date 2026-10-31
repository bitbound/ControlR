using System.Threading.RateLimiting;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.RateLimiting;

/// <summary>
/// Bounds how many assist sessions one host may create. The assist side reaches this hub
/// anonymously, so the connection's remote address is the only identity available to partition on.
/// A hub method has no endpoint, so the attribute-based policy cannot reach it and the limiter is
/// acquired inside the hub method.
/// </summary>
public static class AssistSessionCreateRateLimitPolicy
{
  public const int PermitLimit = 20;

  public static Func<HubCallerContext, RateLimitPartition<string>> Create()
  {
    return context =>
    {
      // A hub context carries no HttpContext when the connection was not established over HTTP,
      // which leaves the connection id as the only key. That is a weaker bound, not an absent one.
      var partitionKey =
        context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString()
        ?? context.ConnectionId
        ?? "unknown";

      return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey,
        _ => new FixedWindowRateLimiterOptions
        {
          PermitLimit = PermitLimit,
          Window = TimeSpan.FromMinutes(1),
          QueueLimit = 0,
          AutoReplenishment = true
        });
    };
  }
}
