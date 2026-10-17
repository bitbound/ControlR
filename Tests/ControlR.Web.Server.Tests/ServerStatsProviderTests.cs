using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// <see cref="IServerStatsProvider"/> is registered as scoped, so each request gets its own
/// instance and both server stats controllers share it only within that request.
/// </summary>
public class ServerStatsProviderTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task GetServerStats_WhenCalledTwice_ReflectsTheDatabaseAtEachCall()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var seedScope = testApp.CreateScope();
    await seedScope.ServiceProvider.CreateTestTenant("First Tenant");
    var provider = seedScope.ServiceProvider.GetRequiredService<IServerStatsProvider>();

    var first = await provider.GetServerStats();

    await seedScope.ServiceProvider.CreateTestTenant("Second Tenant");
    var second = await provider.GetServerStats();

    Assert.True(first.IsSuccess, first.Reason);
    Assert.True(second.IsSuccess, second.Reason);
    Assert.Equal(first.Value.TotalTenants + 1, second.Value.TotalTenants);
  }

  [Fact]
  public async Task GetServerStats_WhenResolvedFromRequestScope_ReturnsCounts()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    await scope.ServiceProvider.CreateTestTenant("Stats Tenant");

    var provider = scope.ServiceProvider.GetRequiredService<IServerStatsProvider>();
    var result = await provider.GetServerStats();

    Assert.True(result.IsSuccess, result.Reason);
    Assert.True(result.Value.TotalTenants >= 1);
  }
}