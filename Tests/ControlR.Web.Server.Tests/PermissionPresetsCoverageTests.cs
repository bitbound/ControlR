using System.Reflection;
using ControlR.Web.Server.Authz.Permissions;

namespace ControlR.Web.Server.Tests;

public class PermissionPresetsCoverageTests
{
  [Fact]
  public void AgentInstaller_ContainsOnlyDeploymentWorkflowPermissions()
  {
    Assert.Equal(
      [
        PermissionNames.AgentInstall,
        PermissionNames.InstallerKeySelfRead,
        PermissionNames.InstallerKeySelfWrite
      ],
      PermissionPresets.GetPermissions(PermissionPresets.AgentInstaller));
  }

  [Fact]
  public void AllPermissionNames_AreCoveredByAtLeastOnePreset()
  {
    var permissionNameFields = typeof(PermissionNames)
      .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
      .Where(f => f.IsLiteral && f.FieldType == typeof(string))
      .Select(f => (string)f.GetValue(null)!)
      .ToArray();

    var allPresetPermissions = PermissionPresets.All.Values
      .SelectMany(x => x)
      .ToHashSet();

    var uncovered = permissionNameFields
      .Where(name => !allPresetPermissions.Contains(name))
      .ToArray();

    Assert.True(
      uncovered.Length == 0,
      $"The following permissions are not in any preset: {string.Join(", ", uncovered)}");
  }

  [Fact]
  public void AllPresetPermissions_ExistInCatalog()
  {
    var unknown = PermissionPresets.All
      .SelectMany(preset => preset.Value.Select(permission => (preset.Key, permission)))
      .Where(item => !PermissionCatalog.Exists(item.permission))
      .ToArray();

    Assert.True(
      unknown.Length == 0,
      $"Preset permissions missing from PermissionCatalog: {string.Join(", ", unknown.Select(x => $"{x.Key}/{x.permission}"))}");
  }

  [Fact]
  public void InstallerKeyManager_ExtendsTheInstallerPresetWithOthersPermissions()
  {
    Assert.Equal(
      [
        PermissionNames.InstallerKeySelfRead,
        PermissionNames.InstallerKeySelfWrite,
        PermissionNames.InstallerKeyOthersRead,
        PermissionNames.InstallerKeyOthersWrite,
        PermissionNames.AgentInstall
      ],
      PermissionPresets.GetPermissions(PermissionPresets.InstallerKeyManager));
  }

  [Fact]
  public void PermissionPresets_AllPermissionListsContainNoDuplicates()
  {
    foreach (var (presetName, permissions) in PermissionPresets.All)
    {
      Assert.Equal(
        permissions.Count,
        permissions.Distinct(StringComparer.Ordinal).Count());
    }
  }

  [Fact]
  public void PermissionPresets_AllPresetNamesAreUnique()
  {
    Assert.Equal(
      PermissionPresets.All.Count,
      PermissionPresets.All.Keys.Distinct(StringComparer.Ordinal).Count());
  }
}
