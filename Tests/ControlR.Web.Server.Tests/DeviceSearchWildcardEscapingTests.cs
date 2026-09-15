using System.Net;
using System.Runtime.InteropServices;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Web.Server.Api.Internal;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.DeviceManagement;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// End-to-end coverage, against real PostgreSQL, for issue #224: search text and column-filter
/// values containing <c>%</c> or <c>_</c> must be matched literally rather than being interpreted
/// as <c>LIKE</c> wildcards.
/// </summary>
/// <remarks>
/// Every test here must run with <c>useInMemoryDatabase: false</c>. The <c>ILIKE</c> branches are
/// only reached on a relational provider. The in-memory path uses literal <see cref="string"/>
/// comparisons and would pass regardless of the escaping under test.
/// </remarks>
public class DeviceSearchWildcardEscapingTests(ITestOutputHelper testOutput)
{
  private const string PercentLeadingName = "%lead";
  private const string PercentMiddleName = "a%b";
  private const string PercentTrailingName = "trail%";
  private const string PlainName = "plain";

  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task SearchDevices_ColumnFilterContainsPercent_MatchesOnlyLiteralPercent()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper, useInMemoryDatabase: false);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, presets: PermissionPresets.DeviceSuperUser);
    var deviceManager = services.GetRequiredService<IDeviceManager>();

    await AddDevice(deviceManager, tenant.Id, "Load 100% Done");
    await AddDevice(deviceManager, tenant.Id, "Plain Device A");
    await AddDevice(deviceManager, tenant.Id, "Plain Device B");

    await controller.SetControllerUser(user, services.GetRequiredService<UserManager<AppUser>>());
    await AssertSeedIsVisible(db, expectedCount: 3);

    // Act - exercise the FilterByStringColumn -> ILIKE path, not FilterBySearchText.
    // SearchText is deliberately left unset so only the column-filter branch runs.
    var result = await controller.SearchDevices(
      new InternalDtos.DeviceSearchRequestDto
      {
        FilterDefinitions =
        [
          new DeviceColumnFilter
          {
            PropertyName = nameof(Device.Name),
            Operator = FilterOperator.String.Contains,
            Value = "%"
          }
        ],
        Page = 0,
        PageSize = 20
      },
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    // Assert
    var response = result.Value;
    Assert.NotNull(response);
    Assert.NotNull(response.Items);
    Assert.Equal(1, response.TotalItems);
    Assert.Single(response.Items);
    Assert.Equal("Load 100% Done", response.Items[0].Name);
  }

  [Fact]
  public async Task SearchDevices_SearchTextPercent_MatchesOnlyLiteralPercent()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper, useInMemoryDatabase: false);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, presets: PermissionPresets.DeviceSuperUser);
    var deviceManager = services.GetRequiredService<IDeviceManager>();

    await AddDevice(deviceManager, tenant.Id, "Load 100% Done");
    await AddDevice(deviceManager, tenant.Id, "Plain Device A");
    await AddDevice(deviceManager, tenant.Id, "Plain Device B");

    await controller.SetControllerUser(user, services.GetRequiredService<UserManager<AppUser>>());
    await AssertSeedIsVisible(db, expectedCount: 3);

    // Act - a lone "%" must match only a literal percent sign, not every row.
    var result = await controller.SearchDevices(
      new InternalDtos.DeviceSearchRequestDto
      {
        SearchText = "%",
        Page = 0,
        PageSize = 20
      },
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    // Assert
    var response = result.Value;
    Assert.NotNull(response);
    Assert.NotNull(response.Items);
    Assert.Equal(1, response.TotalItems);
    Assert.Single(response.Items);
    Assert.Equal("Load 100% Done", response.Items[0].Name);
  }

  [Fact]
  public async Task SearchDevices_SearchTextUnderscore_MatchesOnlyLiteralUnderscore()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper, useInMemoryDatabase: false);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, presets: PermissionPresets.DeviceSuperUser);
    var deviceManager = services.GetRequiredService<IDeviceManager>();

    await AddDevice(deviceManager, tenant.Id, "host_1");
    // Before the fix "_" was a single-character wildcard, so both of these also matched "host_1".
    await AddDevice(deviceManager, tenant.Id, "hostX1");
    await AddDevice(deviceManager, tenant.Id, "hostZ1");

    await controller.SetControllerUser(user, services.GetRequiredService<UserManager<AppUser>>());
    await AssertSeedIsVisible(db, expectedCount: 3);

    // Act - "_" must match one literal underscore, not "any single character".
    var result = await controller.SearchDevices(
      new InternalDtos.DeviceSearchRequestDto
      {
        SearchText = "host_1",
        Page = 0,
        PageSize = 20
      },
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    // Assert
    var response = result.Value;
    Assert.NotNull(response);
    Assert.NotNull(response.Items);
    Assert.Equal(1, response.TotalItems);
    Assert.Single(response.Items);
    Assert.Equal("host_1", response.Items[0].Name);
  }

  [Theory]
  [InlineData(FilterOperator.String.Contains, $"{PercentLeadingName}|{PercentMiddleName}|{PercentTrailingName}")]
  [InlineData(FilterOperator.String.StartsWith, PercentLeadingName)]
  [InlineData(FilterOperator.String.EndsWith, PercentTrailingName)]
  [InlineData(FilterOperator.String.Equal, "")]
  [InlineData(FilterOperator.String.NotContains, PlainName)]
  [InlineData(FilterOperator.String.NotEqual, $"{PercentLeadingName}|{PercentMiddleName}|{PlainName}|{PercentTrailingName}")]
  public async Task SearchDevices_StringOperators_WithPercentValue_MatchLiterally(string filterOperator, string expectedNames)
  {
    // Arrange - the seeds place a literal "%" at the start, end, and middle of a name so that the
    // wildcard side each operator adds is pinned. If an operator put its wildcards on the wrong
    // side, StartsWith and EndsWith would return each other's expected set.
    // testDatabaseName is passed explicitly because TestAppBuilder defaults it to
    // [CallerMemberName], which is identical for every case of a theory. Sharing one database
    // makes the cases collide on the seeded Identity user.
    await using var testApp = await TestAppBuilder.CreateTestApp(
      _testOutputHelper,
      testDatabaseName: $"wildcard-operators-{filterOperator}",
      useInMemoryDatabase: false);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, presets: PermissionPresets.DeviceSuperUser);
    var deviceManager = services.GetRequiredService<IDeviceManager>();

    await AddDevice(deviceManager, tenant.Id, PercentLeadingName);
    await AddDevice(deviceManager, tenant.Id, PercentTrailingName);
    await AddDevice(deviceManager, tenant.Id, PercentMiddleName);
    await AddDevice(deviceManager, tenant.Id, PlainName);

    await controller.SetControllerUser(user, services.GetRequiredService<UserManager<AppUser>>());
    await AssertSeedIsVisible(db, expectedCount: 4);

    // Act
    var result = await controller.SearchDevices(
      new InternalDtos.DeviceSearchRequestDto
      {
        FilterDefinitions =
        [
          new DeviceColumnFilter
          {
            PropertyName = nameof(Device.Name),
            Operator = filterOperator,
            Value = "%"
          }
        ],
        Page = 0,
        PageSize = 20
      },
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    // Assert
    var response = result.Value;
    Assert.NotNull(response);
    Assert.NotNull(response.Items);
    var actualNames = string.Join("|", response.Items.Select(x => x.Name).Order(StringComparer.Ordinal));
    Assert.Equal(expectedNames, actualNames);
  }

  private static async Task AddDevice(IDeviceManager deviceManager, Guid tenantId, string name)
  {
    var deviceDto = new DeviceUpdateRequestDto(
      Name: name,
      AgentVersion: "1.0.0",
      CpuUtilization: 10,
      Id: Guid.NewGuid(),
      Is64Bit: true,
      OsArchitecture: Architecture.X64,
      Platform: SystemPlatform.Windows,
      ProcessorCount: 4,
      OsDescription: "Windows 10",
      TenantId: tenantId,
      TotalMemory: 8192,
      TotalStorage: 256000,
      UsedMemory: 4096,
      UsedStorage: 128000,
      CurrentUsers: ["TestUser"],
      MacAddresses: ["00:11:22:33:44:55"],
      LocalIpV4: "10.0.0.2",
      LocalIpV6: "fe80::2",
      Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 256000, FreeSpace = 128000 }]);

    var connectionContext = new DeviceConnectionContext(
      ConnectionId: $"test-conn-{Guid.NewGuid():N}",
      RemoteIpAddress: IPAddress.Loopback,
      LastSeen: DateTimeOffset.UtcNow,
      IsOnline: true);

    await deviceManager.AddOrUpdate(deviceDto, connectionContext);
  }

  // Guards against a seeding regression silently neutralizing a test: if the control rows failed
  // to persist, a wildcard pattern would match the one remaining row and the count assertions
  // above would pass without the fix being present.
  private static async Task AssertSeedIsVisible(AppDb db, int expectedCount)
  {
    var actualCount = await db.Devices.CountAsync(TestContext.Current.CancellationToken);

    Assert.Equal(expectedCount, actualCount);
  }
}
