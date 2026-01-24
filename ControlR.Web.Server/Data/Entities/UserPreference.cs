using System.ComponentModel.DataAnnotations;
using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Entities;

public class UserPreference : TenantEntityBase
{
  [StringLength(100)]
  [RegularExpression("^[a-zA-Z0-9-]+$", ErrorMessage = "Preference name can only contain letters, numbers, and hyphens.")]
  public required string Name { get; set; }

  public AppUser? User { get; set; }
  public Guid UserId { get; set; }

  [StringLength(100)]
  [RegularExpression("^[a-zA-Z0-9 _-]+$", ErrorMessage = "Preference values can only contain letters, numbers, hyphens, underscores, and spaces.")]
  public required string Value { get; set; }
}
