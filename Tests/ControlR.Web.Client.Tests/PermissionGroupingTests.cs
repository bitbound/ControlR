using ControlR.Libraries.Api.Contracts.Authz;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Web.Client.Helpers;

namespace ControlR.Web.Client.Tests;

/// <summary>
/// Pins how the permission picker groups the catalog it is served. The server supplies each entry's
/// category label, so grouping reads that label and never parses a permission name.
/// </summary>
public class PermissionGroupingTests
{
  [Fact]
  public void GroupForDisplay_GroupsByCategoryLabelNotTheNamePrefix()
  {
    var groups = PermissionGrouping.GroupForDisplay(
    [
      Entry("widget.read", "Devices"),
      Entry(PermissionNames.DeviceRead, "Devices"),
    ]);

    var devices = Assert.Single(groups);

    Assert.Equal("Devices", devices.Label);
    Assert.Equal([PermissionNames.DeviceRead, "widget.read"], devices.Entries.Select(entry => entry.Name));
  }

  [Fact]
  public void GroupForDisplay_OrdersGroupsByLabelAndEntriesByName()
  {
    var groups = PermissionGrouping.GroupForDisplay(
    [
      Entry(PermissionNames.ServerTenantsRead, "Servers"),
      Entry(PermissionNames.AgentInstall, "Agents"),
      Entry("device.Zebra.Read", "Devices"),
      Entry("device.alpha.read", "Devices"),
      Entry(PermissionNames.UserGroupAssignUsers, "User Groups"),
    ]);

    Assert.Equal(["Agents", "Devices", "Servers", "User Groups"], groups.Select(group => group.Label));

    Assert.Equal(
      ["device.alpha.read", "device.Zebra.Read"],
      groups.Single(group => group.Label == "Devices").Entries.Select(entry => entry.Name));
  }

  /// <summary>
  /// Entries that share a label land in one group even when their names do not share a prefix.
  /// </summary>
  [Fact]
  public void GroupForDisplay_WhenLabelsMatch_MergesEntriesAcrossNamePrefixes()
  {
    var groups = PermissionGrouping.GroupForDisplay(
    [
      Entry(PermissionNames.DeviceRead, "Devices"),
      Entry(PermissionNames.DeviceGroupAssignDevices, "Devices"),
    ]);

    var devices = Assert.Single(groups);

    Assert.Equal(
      [PermissionNames.DeviceGroupAssignDevices, PermissionNames.DeviceRead],
      devices.Entries.Select(entry => entry.Name));
  }

  [Fact]
  public void GroupForDisplay_WhenThereAreNoEntries_ReturnsNoGroups()
  {
    Assert.Empty(PermissionGrouping.GroupForDisplay([]));
  }

  private static PermissionCatalogEntryDto Entry(string name, string categoryLabel) =>
    new(name, name, categoryLabel, "Test description.", [PermissionScopeKind.Tenant], true);
}
