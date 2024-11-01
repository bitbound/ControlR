using ControlR.Libraries.Shared.Enums;
using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Entities;

public class Tag : TenantEntityBase
{
  public required string Name { get; set; }
  public TagType Type { get; set; }
  public List<AppUser>? Users { get; set; }
  public List<Device>? Devices { get; set; }
}
