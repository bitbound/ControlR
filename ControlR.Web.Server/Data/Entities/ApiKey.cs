using System.ComponentModel.DataAnnotations;
using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Entities;

public class ApiKey : TenantEntityBase
{
  [Required]
  [StringLength(256)]
  public required string FriendlyName { get; set; }

  [Required]
  [StringLength(256)]
  public required string HashedKey { get; set; }
  public DateTimeOffset CreatedOn { get; set; }
  public DateTimeOffset? LastUsed { get; set; }
}
