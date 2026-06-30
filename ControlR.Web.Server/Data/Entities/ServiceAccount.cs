using System.ComponentModel.DataAnnotations;
using ControlR.Web.Server.Data.Entities.Bases;
using ControlR.Web.Server.Data.Enums;

namespace ControlR.Web.Server.Data.Entities;

/// <summary>
/// A non-interactive automation principal. Server-scoped accounts (<see cref="ServiceAccountKind.Server"/>)
/// have a null <see cref="TenantId"/> and operate across tenants; tenant-scoped accounts
/// (<see cref="ServiceAccountKind.Tenant"/>) are confined to a single <see cref="Tenant"/>.
/// </summary>
public class ServiceAccount : EntityBase
{
  public ServiceAccountKind Kind { get; set; }

  /// <summary>
  /// The owning tenant id. Null for <see cref="ServiceAccountKind.Server"/> accounts.
  /// </summary>
  public Guid? TenantId { get; set; }

  public Tenant? Tenant { get; set; }

  [StringLength(100)]
  public required string Name { get; set; }

  [StringLength(500)]
  public string? Description { get; set; }

  public bool IsEnabled { get; set; } = true;

  public List<ServiceAccountCredential> Credentials { get; set; } = [];
}