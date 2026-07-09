using System.ComponentModel.DataAnnotations.Schema;
using ControlR.Web.Server.Data.Entities.Bases;
using ControlR.Web.Server.Data.Enums;

namespace ControlR.Web.Server.Data.Entities;

public class AppUser : IdentityUser<Guid>, ITenantEntityBase
{
  public AccountType AccountType { get; set; }

  [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
  public DateTimeOffset CreatedAt { get; set; }
  public bool IsOnline { get; set; }
  public DateTimeOffset? LastLogin { get; set; }
  public List<PersonalAccessToken>? PersonalAccessTokens { get; set; }
  public bool RequirePasswordChange { get; set; }
  public List<Tag>? Tags { get; set; }
  public Tenant? Tenant { get; set; }
  public Guid TenantId { get; set; }
  public List<UserPreference>? UserPreferences { get; set; }
  public List<IdentityUserRole<Guid>>? UserRoles { get; set; }
  public List<UserStorageItem>? UserStorageItems { get; set; }
}

