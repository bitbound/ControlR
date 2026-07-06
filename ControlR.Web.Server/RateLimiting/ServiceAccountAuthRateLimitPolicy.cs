using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Options;
using Microsoft.AspNetCore.Http;
using System.Threading.RateLimiting;

namespace ControlR.Web.Server.RateLimiting;

public static class ServiceAccountAuthRateLimitPolicy
{
  public static Func<HttpContext, RateLimitPartition<string>> Create(AppOptions appOptions)
  {
    return httpContext =>
    {
      if (!httpContext.Request.Headers.TryGetValue(ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName, out var authHeaderValues))
      {
        return RateLimitPartition.GetNoLimiter("service-account-auth:none");
      }

      if (appOptions.ServiceAccountAuthFailureLimit <= 0 || appOptions.ServiceAccountAuthFailureWindowMinutes <= 0)
      {
        return RateLimitPartition.GetNoLimiter("service-account-auth:disabled");
      }

      var authHeader = authHeaderValues.FirstOrDefault()?.Trim();
      var keyPart = string.IsNullOrWhiteSpace(authHeader)
        ? "unknown"
        : authHeader.Split(':', 2).FirstOrDefault() ?? "unknown";
      var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
      var partitionKey = $"{remoteIp}:{keyPart}";

      return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey,
        _ => new FixedWindowRateLimiterOptions
        {
          PermitLimit = appOptions.ServiceAccountAuthFailureLimit,
          Window = TimeSpan.FromMinutes(appOptions.ServiceAccountAuthFailureWindowMinutes),
          QueueLimit = 0,
          AutoReplenishment = true,
        });
    };
  }
}