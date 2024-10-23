using System.ComponentModel.DataAnnotations;

namespace ControlR.Web.Server.Data.Entities;

public class UserPreference : EntityBase
{
  [StringLength(100)]
  public required string Name { get; set; }

  public Guid UserId { get; set; }
  public AppUser? User { get; set; }

  [StringLength(100)]
  public required string Value { get; set; }
}
