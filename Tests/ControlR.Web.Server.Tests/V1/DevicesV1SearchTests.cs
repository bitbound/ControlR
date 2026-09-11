using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// Search-envelope parity with the superseded internal endpoint: the access-scope-presence flag,
/// the tag/online filter counts, and the customer fields that the device grids render.
/// </summary>
public class DevicesV1SearchTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task GetDevice_LoadsCustomerName()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var (controller, tenant, _) = await scope.CreateControllerWithTestData<DevicesController>(
      userEmail: "dev-get-customer@test.local",
      presets: PermissionPresets.DeviceSuperUser);

    var appDb = services.GetRequiredService<AppDb>();
    var customer = new Customer { Name = "GetCustomer Co", TenantId = tenant.Id };
    appDb.Customers.Add(customer);
    var device = await services.CreateTestDevice(tenant.Id);
    device = await appDb.Devices.FirstAsync(x => x.Id == device.Id, TestContext.Current.CancellationToken);
    device.CustomerId = customer.Id;
    await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);

    var result = await controller.GetDevice(
      appDb,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>(),
      device.Id,
      TestContext.Current.CancellationToken);

    var dto = Assert.IsType<V1Dtos.DeviceResponseDto>(result.Value);
    Assert.Equal(customer.Id, dto.CustomerId);
    Assert.Equal("GetCustomer Co", dto.CustomerName);
  }

  [Fact]
  public async Task SearchDevices_ReturnsFilterCountsAnyFlagAndCustomerFields()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var (controller, tenant, _) = await scope.CreateControllerWithTestData<DevicesController>(
      userEmail: "dev-search@test.local",
      presets: PermissionPresets.DeviceSuperUser);

    var appDb = services.GetRequiredService<AppDb>();
    var customer = new Customer { Name = "Acme Devices", TenantId = tenant.Id };
    var tag = new Tag { Name = "lab", TenantId = tenant.Id };
    appDb.Customers.Add(customer);
    appDb.Tags.Add(tag);

    var taggedDevice = await services.CreateTestDevice(tenant.Id);
    var plainDevice = await services.CreateTestDevice(tenant.Id);

    taggedDevice = await appDb.Devices.FirstAsync(x => x.Id == taggedDevice.Id, TestContext.Current.CancellationToken);
    taggedDevice.CustomerId = customer.Id;
    taggedDevice.Tags = [tag];
    await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);

    var result = await controller.SearchDevices(
      new V1Dtos.DeviceSearchRequestDto { Page = 0, PageSize = 10 },
      appDb,
      services.GetRequiredService<IAgentVersionProvider>(),
      NullLogger<DevicesController>.Instance,
      TestContext.Current.CancellationToken);

    var response = Assert.IsType<V1Dtos.DeviceSearchResponseDto>(result.Value);

    Assert.True(response.AnyDevicesForUser);
    Assert.Equal(2, response.TotalItems);
    Assert.Equal(2, response.FilterCounts.OnlineDevices);
    Assert.Equal(0, response.FilterCounts.OfflineDevices);
    Assert.Equal(1, response.FilterCounts.TaggedDevices);
    Assert.Equal(1, response.FilterCounts.UntaggedDevices);

    var taggedDto = Assert.Single(response.Items!, x => x.Id == taggedDevice.Id);
    Assert.Equal(customer.Id, taggedDto.CustomerId);
    Assert.Equal("Acme Devices", taggedDto.CustomerName);

    var plainDto = Assert.Single(response.Items!, x => x.Id == plainDevice.Id);
    Assert.Null(plainDto.CustomerId);
    Assert.Null(plainDto.CustomerName);
  }

  [Fact]
  public async Task SearchDevices_WhenCallerHasNoDevices_ReturnsEmptyEnvelope()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    // A user with no permission assignments sees an empty access scope, so the presence
    // flag stays false even though the tenant has a device.
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<DevicesController>(
      userEmail: "dev-search-none@test.local");
    await services.CreateTestDevice(tenant.Id);

    var result = await controller.SearchDevices(
      new V1Dtos.DeviceSearchRequestDto { Page = 0, PageSize = 10 },
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<IAgentVersionProvider>(),
      NullLogger<DevicesController>.Instance,
      TestContext.Current.CancellationToken);

    var response = Assert.IsType<V1Dtos.DeviceSearchResponseDto>(result.Value);

    Assert.False(response.AnyDevicesForUser);
    Assert.Equal(0, response.TotalItems);
    Assert.Equal(0, response.FilterCounts.OnlineDevices);
  }
}
