namespace ControlR.Web.Server.Data.Entities;

/// <summary>
/// An IdentityRole that includes skip-level navigation properties to related entities.
/// </summary>
public class AppRole : IdentityRole<Guid>
{
  public List<IdentityUserRole<Guid>>? UserRoles { get; set; }
}
