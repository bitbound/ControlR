using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Options;
using ControlR.Web.Server.RateLimiting;
using Microsoft.AspNetCore.Http;
using System.Threading.RateLimiting;

namespace ControlR.Web.Server.Tests;

public class ServiceAccountAuthRateLimitPolicyTests
{
  [Fact]
  public async Task GlobalLimiter_LimitsRepeatedServiceAccountHeaderRequests()
  {
    var limiter = PartitionedRateLimiter.Create<HttpContext, string>(
      ServiceAccountAuthRateLimitPolicy.Create(new AppOptions
      {
        ServiceAccountAuthFailureLimit = 2,
        ServiceAccountAuthFailureWindowMinutes = 1,
      }));

    var context = new DefaultHttpContext();
    context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
    context.Request.Headers[ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName] = "abc123:secret";

    using var lease1 = await limiter.AcquireAsync(context, 1, TestContext.Current.CancellationToken);
    using var lease2 = await limiter.AcquireAsync(context, 1, TestContext.Current.CancellationToken);
    using var lease3 = await limiter.AcquireAsync(context, 1, TestContext.Current.CancellationToken);

    Assert.True(lease1.IsAcquired);
    Assert.True(lease2.IsAcquired);
    Assert.False(lease3.IsAcquired);
  }
}