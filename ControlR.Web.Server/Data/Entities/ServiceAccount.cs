using System.ComponentModel.DataAnnotations;
using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Entities;

/// <summary>
/// A non-interactive automation principal. Server-scoped accounts (<see cref="ServiceAccountKind.Server"/>)
/// have a null <see cref="TenantId"/> and operate across tenants; tenant-scoped accounts
/// (<see cref="ServiceAccountKind.Tenant"/>) are confined to a single <see cref="Tenant"/>.
/// </summary>
public class ServiceAccount : EntityBase
{
  /// <summary>
  /// How a server-scoped account is evaluated during permission checks. Explicitly set at creation;
  /// never inferred from assignment rows. Not meaningful for tenant-scoped accounts, which never bypass.
  /// </summary>
  public ServiceAccountAccessMode AccessMode { get; set; } = ServiceAccountAccessMode.Restricted;

  public List<ServiceAccountCredential> Credentials { get; set; } = [];

  [StringLength(500)]
  public string? Description { get; set; }
  public bool IsEnabled { get; set; } = true;
  public ServiceAccountKind Kind { get; set; }
  
  [StringLength(100)]
  public required string Name { get; set; }
  
  public Tenant? Tenant { get; set; }
  
  /// <summary>
  /// The owning tenant id. Null for <see cref="ServiceAccountKind.Server"/> accounts.
  /// </summary>
  public Guid? TenantId { get; set; }
}