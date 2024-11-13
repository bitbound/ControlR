namespace ControlR.Web.Server.Data.Entities;

public class AppRole : IdentityRole<Guid>
{
  public List<IdentityUserRole<Guid>>? UserRoles { get; set; }
}
