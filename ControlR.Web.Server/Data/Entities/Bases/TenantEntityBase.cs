namespace ControlR.Web.Server.Data.Entities.Bases;

public class TenantEntityBase :EntityBase
{
  public Guid TenantId { get; set; }
  public Tenant? Tenant { get; set; }
}
