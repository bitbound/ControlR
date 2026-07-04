using System.ComponentModel.DataAnnotations;
using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Entities;

public class UserStorageItem : EntityBase
{
  public const int MaxValueLength = 2048;

  [StringLength(256)]
  [RegularExpression("^[a-zA-Z0-9-]+$", ErrorMessage = "Storage key can only contain letters, numbers, and hyphens.")]
  public required string Key { get; set; }

  public AppUser? User { get; set; }
  public Guid UserId { get; set; }

  [MaxLength(MaxValueLength)]
  public required string Value { get; set; }
}
