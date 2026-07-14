using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.Tenants;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class TenantProvisioningServiceTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task CreateTenant_CreatesTenantInDatabase()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    var service = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();
    await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

    var result = await service.CreateTenant(new CreateTenantRequestDto("DB Tenant"), TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess);
    var tenant = await appDb.Tenants.FindAsync([result.Value.TenantId], TestContext.Current.CancellationToken);
    Assert.NotNull(tenant);
    Assert.Equal("DB Tenant", tenant.Name);
  }

  [Fact]
  public async Task CreateTenant_OnEmptyName_ReturnsFailure()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    var service = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();

    var result = await service.CreateTenant(new CreateTenantRequestDto(""), TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Contains("Tenant name is required", result.Reason);
  }

  [Fact]
  public async Task CreateTenant_OnWhiteSpaceName_ReturnsFailure()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    var service = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();

    var result = await service.CreateTenant(new CreateTenantRequestDto("   "), TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Contains("Tenant name is required", result.Reason);
  }

  [Fact]
  public async Task CreateTenant_WithValidName_ReturnsSuccess()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    var service = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();

    var result = await service.CreateTenant(new CreateTenantRequestDto("New Tenant"), TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess);
    Assert.NotEqual(Guid.Empty, result.Value.TenantId);
    Assert.Equal("New Tenant", result.Value.TenantName);
  }

  [Fact]
  public async Task GetTenant_EmptyId_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    var service = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();

    var result = await service.GetTenant(Guid.Empty, TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Contains("Tenant not found", result.Reason);
  }

  [Fact]
  public async Task GetTenant_ExistingId_ReturnsSuccess()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    var service = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();
    await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

    var existing = new Tenant { Id = Guid.NewGuid(), Name = "Existing Tenant" };
    appDb.Tenants.Add(existing);
    await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);

    var result = await service.GetTenant(existing.Id, TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess);
    Assert.Equal(existing.Id, result.Value.TenantId);
    Assert.Equal("Existing Tenant", result.Value.TenantName);
  }

  [Fact]
  public async Task GetTenant_NonExistentId_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    var service = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();

    var result = await service.GetTenant(Guid.NewGuid(), TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Contains("Tenant not found", result.Reason);
  }
}