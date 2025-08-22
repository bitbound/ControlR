using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Entities;

public class ApiKey : TenantEntityBase
{
  public required string FriendlyName { get; set; }
  public required string HashedKey { get; set; }
  public DateTimeOffset CreatedOn { get; set; }
  public DateTimeOffset? LastUsed { get; set; }
}
