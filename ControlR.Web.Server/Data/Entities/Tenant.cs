namespace ControlR.Web.Server.Data.Entities;

public class Tenant : EntityBase
{
  public string? Name { get; set; }

  public List<Device>? Devices { get; set; }
  public List<AppUser>? Users { get; set; }
}
