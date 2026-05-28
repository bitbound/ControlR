using ControlR.Libraries.Api.Contracts.Constants;
using Microsoft.AspNetCore.Http;
using System.Threading.RateLimiting;

namespace ControlR.Web.Server.RateLimiting;

public static class AnonymousAuthRateLimitPolicy
{
  public const string PolicyName = nameof(AnonymousAuthRateLimitPolicy);

  public static Func<HttpContext, RateLimitPartition<string>> Create()
  {
    return httpContext =>
    {
      var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
      var path = httpContext.Request.Path.Value ?? HttpConstants.AuthEndpoint;
      var partitionKey = $"{remoteIp}:{path}";

      return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey,
        _ => new FixedWindowRateLimiterOptions
        {
          PermitLimit = 20,
          Window = TimeSpan.FromMinutes(1),
          QueueLimit = 0,
          AutoReplenishment = true
        });
    };
  }
}