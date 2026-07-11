using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Options;
using ControlR.Web.Server.RateLimiting;
using Microsoft.AspNetCore.Http;
using System.Threading.RateLimiting;

namespace ControlR.Web.Server.Tests.V0;

public class ServiceAccountAuthRateLimitPolicyTests
{
  [Fact]
  public async Task DifferentCredentialPrefixes_GetIndependentRateLimits()
  {
    var limiter = PartitionedRateLimiter.Create<HttpContext, string>(
      ServiceAccountAuthRateLimitPolicy.Create(new AppOptions
      {
        ServiceAccountAuthFailureLimit = 2,
        ServiceAccountAuthFailureWindowMinutes = 60,
      }));

    var context = new DefaultHttpContext
    {
      Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.1") }
    };

    var cred1Header = "credPrefixAAA:secret1";
    var cred2Header = "credPrefixBBB:secret2";

    context.Request.Headers[ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName] = cred1Header;
    using var lease1a = await limiter.AcquireAsync(context, 1, TestContext.Current.CancellationToken);
    using var lease1b = await limiter.AcquireAsync(context, 1, TestContext.Current.CancellationToken);
    using var lease1c = await limiter.AcquireAsync(context, 1, TestContext.Current.CancellationToken);

    Assert.True(lease1a.IsAcquired);
    Assert.True(lease1b.IsAcquired);
    Assert.False(lease1c.IsAcquired);

    context.Request.Headers[ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName] = cred2Header;
    using var lease2a = await limiter.AcquireAsync(context, 1, TestContext.Current.CancellationToken);
    using var lease2b = await limiter.AcquireAsync(context, 1, TestContext.Current.CancellationToken);
    using var lease2c = await limiter.AcquireAsync(context, 1, TestContext.Current.CancellationToken);

    Assert.True(lease2a.IsAcquired);
    Assert.True(lease2b.IsAcquired);
    Assert.False(lease2c.IsAcquired);
  }

  [Fact]
  public async Task DifferentIps_GetIndependentRateLimits()
  {
    var limiter = PartitionedRateLimiter.Create<HttpContext, string>(
      ServiceAccountAuthRateLimitPolicy.Create(new AppOptions
      {
        ServiceAccountAuthFailureLimit = 2,
        ServiceAccountAuthFailureWindowMinutes = 60,
      }));

    var authHeader = "commonPrefix:secret1";

    var context1 = new DefaultHttpContext
    {
      Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.1") }
    };
    context1.Request.Headers[ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName] = authHeader;

    var context2 = new DefaultHttpContext
    {
      Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.2") }
    };
    context2.Request.Headers[ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName] = authHeader;

    using var lease1a = await limiter.AcquireAsync(context1, 1, TestContext.Current.CancellationToken);
    using var lease1b = await limiter.AcquireAsync(context1, 1, TestContext.Current.CancellationToken);
    using var lease1c = await limiter.AcquireAsync(context1, 1, TestContext.Current.CancellationToken);

    using var lease2a = await limiter.AcquireAsync(context2, 1, TestContext.Current.CancellationToken);
    using var lease2b = await limiter.AcquireAsync(context2, 1, TestContext.Current.CancellationToken);
    using var lease2c = await limiter.AcquireAsync(context2, 1, TestContext.Current.CancellationToken);

    Assert.True(lease1a.IsAcquired);
    Assert.True(lease1b.IsAcquired);
    Assert.False(lease1c.IsAcquired);

    Assert.True(lease2a.IsAcquired);
    Assert.True(lease2b.IsAcquired);
    Assert.False(lease2c.IsAcquired);
  }

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