namespace ControlR.Web.Server.Data.Entities.Bases;

public interface ITenantEntityBase : IEntityBase
{
  Tenant? Tenant { get; set; }
  Guid TenantId { get; set; }
}

public class TenantEntityBase : EntityBase, ITenantEntityBase
{
  public Tenant? Tenant { get; set; }
  public Guid TenantId { get; set; }
}
