using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Client.Authz;

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
        NormalizedName = RoleNames.ServerAdministrator.ToUpper(),
        ConcurrencyStamp = "d6b798d2-a7f0-492b-a6ad-7eba9b1e3beb",
      },
      new AppRole
      {
        Id = DeterministicGuid.Create(2),
        Name = RoleNames.TenantAdministrator,
        NormalizedName = RoleNames.TenantAdministrator.ToUpper(),
        ConcurrencyStamp = "b23bdf83-ecc8-4ca2-ba24-dc1780bfefc6",
      },
      new AppRole
      {
        Id = DeterministicGuid.Create(3),
        Name = RoleNames.DeviceSuperUser,
        NormalizedName = RoleNames.DeviceSuperUser.ToUpper(),
        ConcurrencyStamp = "0b692fe4-63e1-4a99-b021-4fc48ed81f4c",
      },
      new AppRole
      {
        Id = DeterministicGuid.Create(4),
        Name = RoleNames.AgentInstaller,
        NormalizedName = RoleNames.AgentInstaller.ToUpper(),
        ConcurrencyStamp = "ccfd2843-8a06-43d4-9bf3-6110b4e65900",
      },
      new AppRole
      {
        Id = DeterministicGuid.Create(5),
        Name = RoleNames.InstallerKeyManager,
        NormalizedName = RoleNames.InstallerKeyManager.ToUpper(),
        ConcurrencyStamp = "a7e1a339-19c3-4d44-97e3-239636906a45",
      }
    ];
  }
}
