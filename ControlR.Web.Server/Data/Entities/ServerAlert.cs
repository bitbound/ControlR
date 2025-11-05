using System.ComponentModel.DataAnnotations;
using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Entities;

public class ServerAlert : EntityBase
{
  public required bool IsDismissable { get; set; }

  public required bool IsEnabled { get; set; }

  public required bool IsSticky { get; set; }

  [StringLength(500)]
  public required string Message { get; set; }

  public required MessageSeverity Severity { get; set; }
}