using System.ComponentModel.DataAnnotations;
using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Entities;

public class PersonalAccessToken : TenantEntityBase
{
  [Required]
  [StringLength(256)]
  public required string HashedKey { get; set; }
  public DateTimeOffset? LastUsed { get; set; }
  [Required]
  [StringLength(256)]
  public required string Name { get; set; }
  public AppUser? User { get; set; }
  
  [Required]
  public required Guid UserId { get; set; }
}
