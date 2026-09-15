using System.Runtime.InteropServices;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Web.Client.Helpers;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.Web.Client.Tests;

public class DeviceDisplayTests
{
  private const string DeviceIdPrefix = "a1b2c3d4";

  [Fact]
  public void GetAliasDisplay_NullAlias_ReturnsDash()
  {
    var device = CreateDevice("WS-01", alias: null);

    Assert.Equal("—", DeviceDisplay.GetAliasDisplay(device));
  }

  [Fact]
  public void GetAliasDisplay_WithAlias_ReturnsAlias()
  {
    var device = CreateDevice("WS-01", alias: "Front Desk");

    Assert.Equal("Front Desk", DeviceDisplay.GetAliasDisplay(device));
  }

  [Fact]
  public void GetCustomerDisplay_NullCustomer_ReturnsNone()
  {
    var device = CreateDevice("WS-01", customerName: null);

    Assert.Equal("—", DeviceDisplay.GetCustomerDisplay(device));
  }

  [Fact]
  public void GetCustomerDisplay_WhitespaceCustomer_ReturnsNone()
  {
    var device = CreateDevice("WS-01", customerName: "  ");

    Assert.Equal("—", DeviceDisplay.GetCustomerDisplay(device));
  }

  [Fact]
  public void GetCustomerDisplay_WithCustomer_ReturnsName()
  {
    var device = CreateDevice("WS-01", customerName: "Acme");

    Assert.Equal("Acme", DeviceDisplay.GetCustomerDisplay(device));
  }

  [Fact]
  public void GetDisplayName_NameAliasCustomer_ReturnsAllSegments()
  {
    var device = CreateDevice("WS-01", alias: "Front Desk", customerName: "Acme");

    var result = DeviceDisplay.GetFullDisplayName(device);

    Assert.Equal($"WS-01  (Customer: Acme  |  Alias: Front Desk  |  Device ID: {DeviceIdPrefix}...)", result);
  }

  [Fact]
  public void GetDisplayName_NullAlias_RendersDash()
  {
    var device = CreateDevice("WS-01", alias: null, customerName: "Acme");

    var result = DeviceDisplay.GetFullDisplayName(device);

    Assert.Equal($"WS-01  (Customer: Acme  |  Alias: —  |  Device ID: {DeviceIdPrefix}...)", result);
  }

  [Fact]
  public void GetDisplayName_NullCustomer_RendersDash()
  {
    var device = CreateDevice("WS-01", alias: null, customerName: null);

    var result = DeviceDisplay.GetFullDisplayName(device);

    Assert.Equal($"WS-01  (Customer: —  |  Alias: —  |  Device ID: {DeviceIdPrefix}...)", result);
  }

  [Fact]
  public void GetDisplayName_WhitespaceAlias_RendersDash()
  {
    var device = CreateDevice("WS-01", alias: "   ", customerName: "Acme");

    var result = DeviceDisplay.GetFullDisplayName(device);

    Assert.Equal($"WS-01  (Customer: Acme  |  Alias: —  |  Device ID: {DeviceIdPrefix}...)", result);
  }

  [Fact]
  public void GetIdDisplay_ReturnsTruncatedId()
  {
    var device = CreateDevice("WS-01");

    Assert.Equal($"{DeviceIdPrefix}...", DeviceDisplay.GetIdDisplay(device));
  }

  private static DeviceResponseDto CreateDevice(string name, string? alias = null, string? customerName = null)
  {
    return new DeviceResponseDto(
      Name: name,
      AgentVersion: "1.0.0",
      CpuUtilization: 0,
      Id: Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
      Is64Bit: true,
      IsOnline: true,
      LastSeen: DateTimeOffset.UtcNow,
      OsArchitecture: Architecture.X64,
      Platform: SystemPlatform.Windows,
      ProcessorCount: 8,
      ConnectionId: "connection",
      OsDescription: "OS",
      TenantId: Guid.NewGuid(),
      TotalMemory: 16,
      TotalStorage: 512,
      UsedMemory: 8,
      UsedStorage: 256,
      CurrentUsers: [],
      MacAddresses: [],
      PublicIpV4: "1.2.3.4",
      PublicIpV6: "::1",
      LocalIpV4: "10.0.0.1",
      LocalIpV6: "::1",
      Drives: [],
      IsOutdated: false)
    {
      Alias = alias,
      CustomerName = customerName
    };
  }
}
