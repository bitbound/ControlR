using System.ComponentModel.DataAnnotations;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Entities;

public class Script : TenantEntityBase
{
  [Required]
  [StringLength(100)]
  public string Name { get; set; } = string.Empty;

  [StringLength(500)]
  public string Description { get; set; } = string.Empty;

  [Required]
  public string CodeContent { get; set; } = string.Empty;

  public ShellType ShellType { get; set; }

  public int TimeoutSeconds { get; set; } = 300;
}
