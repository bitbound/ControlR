using System.ComponentModel.DataAnnotations;
using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Entities;

public class TenantSetting : TenantEntityBase
{
  [StringLength(100)]
  [RegularExpression("^[a-zA-Z0-9-]+$", ErrorMessage = "Setting name can only contain letters, numbers, and hyphens.")]
  public required string Name { get; set; }

  [StringLength(100)]
  [RegularExpression("^[a-zA-Z0-9-_. ]+$", ErrorMessage = "Setting values can only contain letters, numbers, hyphens, periods, underscores, and spaces.")]
  public required string Value { get; set; }
}
