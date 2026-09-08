using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Web.Client.Components.Dialogs;

namespace ControlR.Web.Client.Tests;

/// <summary>
/// Pins the dialog scope-selection rules against the server write gates: the picker may hide
/// or default to only what <c>ValidateWriteAuthority</c> + the Server-scope invariant accept,
/// and a default selection must never land on a scope that isn't offered.
/// </summary>
public class PermissionScopeSelectionTests
{
  private static readonly IReadOnlyList<PermissionScopeKind> _deviceFamily =
    [PermissionScopeKind.Device, PermissionScopeKind.DeviceGroup, PermissionScopeKind.CustomerTenant, PermissionScopeKind.Tenant, PermissionScopeKind.Server];
  private static readonly IReadOnlyList<PermissionScopeKind> _serverOnly = [PermissionScopeKind.Server];
  private static readonly IReadOnlyList<PermissionScopeKind> _tenantOnly = [PermissionScopeKind.Tenant];

  [Fact]
  public void AvailableScopeKinds_WhenCallerLacksServerAuthority_RemovesServerEvenForDeny()
  {
    var kinds = PermissionScopeSelection.AvailableScopeKinds(_deviceFamily, PermissionEffect.Deny, false, false);
    Assert.DoesNotContain(PermissionScopeKind.Server, kinds);
  }

  [Fact]
  public void AvailableScopeKinds_WhenServerOnlyPermissionForServerAccount_KeepsServer()
  {
    var kinds = PermissionScopeSelection.AvailableScopeKinds(_serverOnly, PermissionEffect.Allow, true, true);
    Assert.Contains(PermissionScopeKind.Server, kinds);
  }

  [Fact]
  public void AvailableScopeKinds_WhenTenantAddressableAllowForTenantBoundPrincipal_RemovesServer()
  {
    var kinds = PermissionScopeSelection.AvailableScopeKinds(_deviceFamily, PermissionEffect.Allow, false, true);
    Assert.DoesNotContain(PermissionScopeKind.Server, kinds);
  }

  [Fact]
  public void BroadestSelectable_AlwaysInsideAvailableScopeKinds()
  {
    foreach (var (allowed, effect, serverTarget, canManage) in new[]
    {
      (_deviceFamily, PermissionEffect.Allow, false, true),
      (_deviceFamily, PermissionEffect.Deny, false, true),
      (_deviceFamily, PermissionEffect.Allow, true, true),
      (_deviceFamily, PermissionEffect.Allow, false, false),
      (_serverOnly, PermissionEffect.Allow, false, true),
      (_tenantOnly, PermissionEffect.Deny, false, false)
    })
    {
      var available = PermissionScopeSelection.AvailableScopeKinds(allowed, effect, serverTarget, canManage);
      var broadest = PermissionScopeSelection.BroadestSelectable(allowed, effect, serverTarget, canManage);
      Assert.True(
        available.Contains(broadest) || broadest == PermissionScopeKind.Tenant && available.Contains(PermissionScopeKind.Tenant),
        $"default {broadest} not offered for allowed={string.Join(",", allowed)}, effect={effect}, serverTarget={serverTarget}, canManage={canManage}");
    }
  }

  [Fact]
  public void BroadestSelectable_WhenPermissionIsServerOnlyForTenantBoundPrincipal_DefaultsToServer()
  {
    // Server is the only legal scope for server administration permissions, and users must be
    // able to hold them (god-mode cell). Defaulting away from Server lands on a scope that is
    // neither offered nor accepted by the API.
    var scope = PermissionScopeSelection.BroadestSelectable(_serverOnly, PermissionEffect.Allow, false, true);
    Assert.Equal(PermissionScopeKind.Server, scope);
  }

  [Fact]
  public void BroadestSelectable_WhenTenantAddressableAllowForServerAccount_DefaultsToServer()
  {
    var scope = PermissionScopeSelection.BroadestSelectable(_deviceFamily, PermissionEffect.Allow, true, true);
    Assert.Equal(PermissionScopeKind.Server, scope);
  }

  [Fact]
  public void BroadestSelectable_WhenTenantAddressableDenyForTenantBoundPrincipal_DefaultsToTenant()
  {
    // Deny makes Server selectable, but it must never be pre-selected for a tenant-bound
    // principal; the org-wide kill switch is an explicit choice.
    var scope = PermissionScopeSelection.BroadestSelectable(_deviceFamily, PermissionEffect.Deny, false, true);
    Assert.Equal(PermissionScopeKind.Tenant, scope);
  }

  [Fact]
  public void BroadestSelectable_WhenTenantOnlyPermission_DefaultsToTenant()
  {
    var scope = PermissionScopeSelection.BroadestSelectable(_tenantOnly, PermissionEffect.Allow, false, false);
    Assert.Equal(PermissionScopeKind.Tenant, scope);
  }
}
