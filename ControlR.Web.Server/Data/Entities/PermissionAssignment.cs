using System.ComponentModel.DataAnnotations;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Entities;

public class PermissionAssignment : EntityBase
{
  [StringLength(50)]
  public string? CreatedByPrincipalId { get; set; }

  [StringLength(50)]
  public string? CreatedByPrincipalType { get; set; }

  public PermissionEffect Effect { get; set; }

  public bool IsEnabled { get; set; } = true;

  [StringLength(500)]
  public string? Notes { get; set; }

  public Guid? OwningTenantId { get; set; }

  [StringLength(150)]
  public required string PermissionName { get; set; }

  public Guid PrincipalId { get; set; }

  public PermissionPrincipalKind PrincipalKind { get; set; }

  public Guid? ScopeId { get; set; }

  public PermissionScopeKind ScopeKind { get; set; }

  /// <summary>
  /// Creates a grant row; server-scoped rows carry no ScopeId or OwningTenantId. The
  /// <paramref name="createdBy"/> descriptor stamps the row's creator attribution; a null
  /// descriptor records a system-created row.
  /// </summary>
  public static PermissionAssignment CreateGrant(
    PermissionPrincipalKind principalKind,
    Guid principalId,
    string permissionName,
    PermissionScopeKind scopeKind,
    Guid? scopeId,
    Guid? owningTenantId,
    PrincipalDescriptor? createdBy,
    PermissionEffect effect = PermissionEffect.Allow,
    string? notes = null,
    bool isEnabled = true) =>
    new()
    {
      PrincipalKind = principalKind,
      PrincipalId = principalId,
      PermissionName = permissionName,
      Effect = effect,
      ScopeKind = scopeKind,
      ScopeId = scopeKind == PermissionScopeKind.Server ? null : scopeId,
      IsEnabled = isEnabled,
      OwningTenantId = scopeKind == PermissionScopeKind.Server ? null : owningTenantId,
      CreatedByPrincipalType = createdBy?.PrincipalType.ToAuthorizationChangeLogActorType() ?? AuthorizationChangeLogActorTypes.System,
      CreatedByPrincipalId = createdBy?.PrincipalId.ToString(),
      Notes = notes
    };
}
