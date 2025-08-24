using System.ComponentModel.DataAnnotations.Schema;
using ControlR.Web.Server.Data.Entities.Bases;
using Microsoft.AspNetCore.Identity;


namespace ControlR.Web.Server.Data.Entities;

public class AppUser : IdentityUser<Guid>, ITenantEntityBase
{
  [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
  public DateTimeOffset CreatedAt { get; set; }
  public bool IsOnline { get; set; }
  public bool IsTemporary { get; set; } = false;
  public List<Tag>? Tags { get; set; }
  public Tenant? Tenant { get; set; }
  public Guid TenantId { get; set; }
  public DateTimeOffset? TemporaryUserExpiresAt { get; set; }
  public List<UserPreference>? UserPreferences { get; set; }
  public List<IdentityUserRole<Guid>>? UserRoles { get; set; }
}

