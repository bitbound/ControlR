using ControlR.Web.Server.Data.Entities.Bases;
using System.ComponentModel.DataAnnotations;

namespace ControlR.Web.Server.Data.Entities;

public class Tag : TenantEntityBase
{
  public List<Device>? Devices { get; set; }
  [StringLength(50)]
  public required string Name { get; set; }

  public TagType Type { get; set; }
  public List<AppUser>? Users { get; set; }
}
