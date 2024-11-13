using System.ComponentModel.DataAnnotations.Schema;
using ControlR.Web.Server.Data.Entities.Bases;
using Microsoft.AspNetCore.Identity;


namespace ControlR.Web.Server.Data.Entities;

public class AppUser : IdentityUser<Guid>, ITenantEntityBase
{
  private DateTimeOffset _createdAt;

  [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
  public DateTimeOffset CreatedAt
  {
    get => _createdAt;
    set
    {
      if (value == default ||
          _createdAt != default)
      {
        return;
      }

      _createdAt = value;
    }
  }

  public List<IdentityUserRole<Guid>>? UserRoles { get; set; }

  public List<Tag>? Tags { get; set; }
  public Tenant? Tenant { get; set; }
  public Guid TenantId { get; set; }
  public List<UserPreference>? UserPreferences { get; set; }
}

