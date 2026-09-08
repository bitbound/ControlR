using ControlR.Web.Server.Authz.Policies;
using ControlR.Web.Server.Authz.Permissions;
using System.Reflection;

namespace ControlR.Web.Server.Tests;

public class PermissionPolicyMapTests
{
  [Fact]
  public void ClientDefinitions_AreExactlyTheTenantAndServerScopedPolicies()
  {
    var expected = PermissionPolicies.Definitions
      .Where(entry => entry.Value.ResourceScopeKind is PermissionScopeKind.Tenant or PermissionScopeKind.Server)
      .Select(entry => entry.Key)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    var actual = PermissionPolicies.ClientDefinitions.Keys
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(expected, actual);
  }

  [Fact]
  public void ClientDefinitions_EveryDeclaredPolicyNameIsProjected()
  {
    var clientGlobalPolicies = new[]
    {
      PolicyNames.RequireAgentInstall,
      PolicyNames.RequireAuthorizationLogsRead,
      PolicyNames.RequireCustomersRead,
      PolicyNames.RequireCustomersWrite,
      PolicyNames.RequireDeviceGroupsRead,
      PolicyNames.RequireInstallerKeyRead,
      PolicyNames.RequirePermissionAssignmentsRead,
      PolicyNames.RequirePermissionAssignmentsWrite,
      PolicyNames.RequireServerAuthorizationLogsRead,
      PolicyNames.RequireServerPermissionsWrite,
      PolicyNames.RequireServerServiceAccountsRead,
      PolicyNames.RequireServerServiceAccountsRotateCredentials,
      PolicyNames.RequireServerSettingsWrite,
      PolicyNames.RequireServerTelemetryRead,
      PolicyNames.RequireServiceAccountRead,
      PolicyNames.RequireServiceAccountRotateCredentials,
      PolicyNames.RequireTagsWrite,
      PolicyNames.RequireTenantSettingsRead,
      PolicyNames.RequireTenantSettingsWrite,
      PolicyNames.RequireTenantUsersWrite,
      PolicyNames.RequireUserGroupsRead,
      PolicyNames.RequireUsersRead,
    };

    var missing = clientGlobalPolicies
      .Where(policyName => !PermissionPolicies.ClientDefinitions.ContainsKey(policyName))
      .ToArray();

    Assert.True(
      missing.Length == 0,
      $"Global client policies missing from ClientDefinitions: {string.Join(", ", missing)}");
  }

  [Fact]
  public void ClientDefinitions_EveryEntryExistsInFullMapAndIsProjected()
  {
    foreach (var (policyName, definition) in PermissionPolicies.ClientDefinitions)
    {
      Assert.True(
        PermissionPolicies.Definitions.TryGetValue(policyName, out var fullDefinition),
        $"Client policy '{policyName}' is missing from Definitions.");

      Assert.Equal(
        fullDefinition.PermissionName,
        definition.PermissionName);
    }
  }

  [Fact]
  public void ClientDefinitions_ProjectedPoliciesAreTenantOrServerScoped()
  {
    foreach (var (policyName, definition) in PermissionPolicies.ClientDefinitions)
    {
      Assert.True(
        definition.ResourceScopeKind is PermissionScopeKind.Tenant or PermissionScopeKind.Server,
        $"Projected policy '{policyName}' has unsupported resource kind '{definition.ResourceScopeKind}'. " +
        "Resource-specific access must never be a global client claim.");
    }
  }

  [Fact]
  public void DeviceResourcePolicies_AllConstantsAreMapped()
  {
    var policyNames = GetPublicStringConstants(typeof(DeviceResourcePolicies));
    var missing = policyNames
      .Where(policyName => !DeviceResourcePolicies.PolicyToPermission.ContainsKey(policyName))
      .ToArray();

    Assert.True(
      missing.Length == 0,
      $"DeviceResourcePolicies constants missing from PolicyToPermission: {string.Join(", ", missing)}");
  }

  [Fact]
  public void DeviceResourcePolicies_AllMappedKeysAreDeclaredConstants()
  {
    var policyNames = GetPublicStringConstants(typeof(DeviceResourcePolicies))
      .ToHashSet(StringComparer.Ordinal);
    var undeclared = DeviceResourcePolicies.PolicyToPermission.Keys
      .Where(policyName => !policyNames.Contains(policyName))
      .ToArray();

    Assert.True(
      undeclared.Length == 0,
      $"DeviceResourcePolicies entries missing from declared constants: {string.Join(", ", undeclared)}");
  }

  [Fact]
  public void PermissionCatalog_AllEntriesHavePermissionNameConstants()
  {
    var permissionNames = GetPublicStringConstants(typeof(PermissionNames))
      .ToHashSet(StringComparer.Ordinal);
    var catalogOnly = PermissionCatalog.All.Keys
      .Where(permissionName => !permissionNames.Contains(permissionName))
      .ToArray();

    Assert.True(
      catalogOnly.Length == 0,
      $"PermissionCatalog entries missing from PermissionNames: {string.Join(", ", catalogOnly)}");
  }

  [Fact]
  public void PermissionNames_AllConstantsExistInCatalog()
  {
    var permissionNames = GetPublicStringConstants(typeof(PermissionNames));
    var missing = permissionNames
      .Where(permissionName => !PermissionCatalog.Exists(permissionName))
      .ToArray();

    Assert.True(
      missing.Length == 0,
      $"PermissionNames constants missing from PermissionCatalog: {string.Join(", ", missing)}");
  }

  [Fact]
  public void PermissionPolicies_AllMappedPolicyKeysHavePolicyNameConstants()
  {
    var policyNames = GetPublicStringConstants(typeof(PolicyNames))
      .ToHashSet(StringComparer.Ordinal);
    var unmapped = PermissionPolicies.PolicyToPermission.Keys
      .Where(policyName => !policyNames.Contains(policyName))
      .ToArray();

    Assert.True(
      unmapped.Length == 0,
      $"PermissionPolicies entries missing from PolicyNames: {string.Join(", ", unmapped)}");
  }

  [Fact]
  public void PolicyNames_AllConstantsAreMapped()
  {
    var policyNames = GetPublicStringConstants(typeof(PolicyNames));
    var missing = policyNames
      .Where(policyName => !PermissionPolicies.PolicyToPermission.ContainsKey(policyName))
      .ToArray();

    Assert.True(
      missing.Length == 0,
      $"PolicyNames constants missing from PermissionPolicies.PolicyToPermission: {string.Join(", ", missing)}");
  }

  [Fact]
  public void PolicyToPermission_Values_AreKnownCatalogPermissions()
  {
    foreach (var (policyName, permissionName) in PermissionPolicies.PolicyToPermission)
    {
      Assert.True(
        PermissionCatalog.Exists(permissionName),
        $"Policy '{policyName}' maps to permission '{permissionName}', which is not in the PermissionCatalog.");
    }

    foreach (var (policyName, permissionName) in DeviceResourcePolicies.PolicyToPermission)
    {
      Assert.True(
        PermissionCatalog.Exists(permissionName),
        $"Device policy '{policyName}' maps to permission '{permissionName}', which is not in the PermissionCatalog.");
    }
  }

  private static IReadOnlyList<string> GetPublicStringConstants(Type type) =>
    type
      .GetFields(BindingFlags.Public | BindingFlags.Static)
      .Where(field => field.IsLiteral && field.FieldType == typeof(string))
      .Select(field => (string)field.GetRawConstantValue()!)
      .ToArray();
}
