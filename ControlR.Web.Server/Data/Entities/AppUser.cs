using Microsoft.AspNetCore.Identity;


namespace ControlR.Web.Server.Data.Entities;

public class AppUser : IdentityUser<Guid>
{
  public Tenant? Tenant { get; set; }
  public Guid TenantId { get; set; }
  public List<UserPreference> UserPreferences { get; init; } = [];
}

