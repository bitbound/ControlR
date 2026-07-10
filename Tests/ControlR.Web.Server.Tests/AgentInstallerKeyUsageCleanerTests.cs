using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Services.AgentInstaller;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class AgentInstallerKeyUsageCleanerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task CleanExpiredUsages_RemovesEntriesOlderThanRetention()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput,
      testDatabaseName: $"{Guid.NewGuid()}",
      extraConfiguration: new Dictionary<string, string?>
      {
        { "AppOptions:AgentInstallerKeyHistoryDays", "1" }
      });

    var backgroundService = testApp.Services.GetRequiredService<AgentInstallerKeyUsageCleanupBackgroundService>();
    var keyManager = testApp.Services.GetRequiredService<IAgentInstallerKeyManager>();
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id);

    var dto = await keyManager.CreateKey(
      tenantId: tenant.Id,
      creatorId: user.Id,
        creatorKind: CreatorKind.User,
      keyType: InstallerKeyType.Persistent,
      allowedUses: null,
      expiration: null,
      friendlyName: "Test Key");

    await keyManager.ValidateAndConsumeKey(dto.Id, dto.KeySecret, Guid.NewGuid());
    await keyManager.ValidateAndConsumeKey(dto.Id, dto.KeySecret, Guid.NewGuid());

    testApp.TimeProvider.Advance(TimeSpan.FromDays(2));

    await keyManager.ValidateAndConsumeKey(dto.Id, dto.KeySecret, Guid.NewGuid());

    var removedCount = await backgroundService.CleanExpiredUsages(TestContext.Current.CancellationToken);

    Assert.Equal(2, removedCount);

    using var scope = testApp.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var usages = await db.AgentInstallerKeyUsages
      .Where(x => x.AgentInstallerKeyId == dto.Id)
      .ToListAsync(TestContext.Current.CancellationToken);

    Assert.Single(usages);
  }

  [Fact]
  public async Task CleanExpiredUsages_WhenRetentionDisabled_DoesNothing()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput,
      testDatabaseName: $"{Guid.NewGuid()}",
      extraConfiguration: new Dictionary<string, string?>
      {
        { "AppOptions:AgentInstallerKeyHistoryDays", "0" }
      });

    var backgroundService = testApp.Services.GetRequiredService<AgentInstallerKeyUsageCleanupBackgroundService>();
    var keyManager = testApp.Services.GetRequiredService<IAgentInstallerKeyManager>();
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id);

    var dto = await keyManager.CreateKey(
      tenantId: tenant.Id,
      creatorId: user.Id,
        creatorKind: CreatorKind.User,
      keyType: InstallerKeyType.Persistent,
      allowedUses: null,
      expiration: null,
      friendlyName: "Test Key");

    await keyManager.ValidateAndConsumeKey(dto.Id, dto.KeySecret, Guid.NewGuid());
    testApp.TimeProvider.Advance(TimeSpan.FromDays(10));

    var removedCount = await backgroundService.CleanExpiredUsages(TestContext.Current.CancellationToken);

    Assert.Equal(0, removedCount);
  }
}