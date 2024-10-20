namespace ControlR.Web.Server.Data.Entities;

public class DeviceGroup : EntityBase
{
  public List<Device>? Devices { get; set; }
  public required string Name { get; set; }
  public Tenant? Tenant { get; set; }
  public Guid TenantId { get; set; }
}
