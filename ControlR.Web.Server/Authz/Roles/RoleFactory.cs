using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Web.Server.Authz.Roles;

public static class RoleFactory
{
  public static AppRole[] GetBuiltInRoles()
  {
    return
    [
      new AppRole
      {
        Id = DeterministicGuid.Create(1),
        Name = RoleNames.ServerAdministrator,
        NormalizedName = RoleNames.ServerAdministrator.ToUpper()
      },
      new AppRole
      {
        Id = DeterministicGuid.Create(2),
        Name = RoleNames.TenantAdministrator,
        NormalizedName = RoleNames.TenantAdministrator.ToUpper()
      },
      new AppRole
      {
        Id = DeterministicGuid.Create(3),
        Name = RoleNames.DeviceSuperUser,
        NormalizedName = RoleNames.DeviceSuperUser.ToUpper()
      },
      new AppRole
      {
        Id = DeterministicGuid.Create(4),
        Name = RoleNames.AgentInstaller,
        NormalizedName = RoleNames.AgentInstaller.ToUpper()
      }
    ];
  }
}
