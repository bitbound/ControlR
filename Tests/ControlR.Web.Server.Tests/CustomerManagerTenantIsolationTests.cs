using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services.Customers;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class CustomerManagerTenantIsolationTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task AssignDevices_CrossTenantDevice_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.App.Services.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<ICustomerManager>();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    var tenantA = await testApp.Services.CreateTestTenant("Tenant A");
    var tenantB = await testApp.Services.CreateTestTenant("Tenant B");
    var actor = await testApp.Services.CreateTestUser(tenantA.Id, $"a-{Guid.NewGuid():N}@t.local");

    var customerA = await CreateCustomer(db, tenantA.Id, "Customer A", actor.Id);
    var deviceB = await testApp.Services.CreateTestDevice(tenantB.Id);

    var result = await manager.AssignDevices(
      customerA.Id,
      [deviceB.Id],
      null,
      tenantA.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenantA.Id, "test"),
      TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.BadRequest, result.ErrorCode);

    var assigned = await db.Devices.AnyAsync(
      x => x.Id == deviceB.Id && x.CustomerId == customerA.Id,
      TestContext.Current.CancellationToken);
    Assert.False(assigned, "Cross-tenant device must not be assigned to a customer.");
  }

  [Fact]
  public async Task Delete_CrossTenantCustomer_ReturnsNotFound_AndDoesNotRemove()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.App.Services.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<ICustomerManager>();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    var tenantA = await testApp.Services.CreateTestTenant("Tenant A");
    var tenantB = await testApp.Services.CreateTestTenant("Tenant B");
    var actor = await testApp.Services.CreateTestUser(tenantA.Id, $"a-{Guid.NewGuid():N}@t.local");

    var crossTenantCustomer = await CreateCustomer(db, tenantB.Id, "Customer B", actor.Id);

    var result = await manager.Delete(
      crossTenantCustomer.Id,
      tenantA.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenantA.Id, "test"),
      TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.NotFound, result.ErrorCode);

    var stillExists = await db.Customers.AnyAsync(
      x => x.Id == crossTenantCustomer.Id,
      TestContext.Current.CancellationToken);
    Assert.True(stillExists, "Cross-tenant customer must not be deleted.");
  }

  [Fact]
  public async Task GetAll_ExcludesOtherTenantCustomers()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.App.Services.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<ICustomerManager>();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    var tenantA = await testApp.Services.CreateTestTenant("Tenant A");
    var tenantB = await testApp.Services.CreateTestTenant("Tenant B");
    var actor = await testApp.Services.CreateTestUser(tenantA.Id, $"a-{Guid.NewGuid():N}@t.local");

    var customerA = await CreateCustomer(db, tenantA.Id, "Customer A", actor.Id);
    await CreateCustomer(db, tenantB.Id, "Customer B", actor.Id);

    var all = await manager.GetAll(tenantA.Id, TestContext.Current.CancellationToken);

    Assert.Contains(all, c => c.Id == customerA.Id);
    Assert.DoesNotContain(all, c => c.Name == "Customer B");
  }

  [Fact]
  public async Task Get_CrossTenantCustomer_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.App.Services.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<ICustomerManager>();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    var tenantA = await testApp.Services.CreateTestTenant("Tenant A");
    var tenantB = await testApp.Services.CreateTestTenant("Tenant B");
    var actor = await testApp.Services.CreateTestUser(tenantA.Id, $"a-{Guid.NewGuid():N}@t.local");

    var customerA = await CreateCustomer(db, tenantA.Id, "Customer A", actor.Id);
    var moved = await CreateCustomer(db, tenantB.Id, "Customer B", actor.Id);

    var result = await manager.Get(moved.Id, tenantA.Id, TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.NotFound, result.ErrorCode);
    Assert.NotNull(customerA);
  }

  [Fact]
  public async Task Update_CrossTenantCustomer_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.App.Services.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<ICustomerManager>();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    var tenantA = await testApp.Services.CreateTestTenant("Tenant A");
    var tenantB = await testApp.Services.CreateTestTenant("Tenant B");
    var actor = await testApp.Services.CreateTestUser(tenantA.Id, $"a-{Guid.NewGuid():N}@t.local");

    var crossTenantCustomer = await CreateCustomer(db, tenantB.Id, "Customer B", actor.Id);

    var result = await manager.Update(
      crossTenantCustomer.Id,
      "Renamed",
      null,
      null,
      tenantA.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenantA.Id, "test"),
      TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.NotFound, result.ErrorCode);
  }

  private static async Task<Customer> CreateCustomer(
    AppDb db,
    Guid tenantId,
    string name,
    Guid actorPrincipalId)
  {
    var customer = new Customer
    {
      Name = name,
      TenantId = tenantId
    };
    db.Customers.Add(customer);
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    return customer;
  }
}
