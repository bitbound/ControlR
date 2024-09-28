using Microsoft.AspNetCore.Identity;


namespace ControlR.Web.Server.Data.Entities;

public class AppUser : IdentityUser<int>
{
  public Tenant? Tenant { get; set; }
  public int TenantId { get; set; }
}

