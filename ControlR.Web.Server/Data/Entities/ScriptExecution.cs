using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Entities;

public class ScriptExecution : TenantEntityBase
{
  public Guid? ScriptId { get; set; }
  public Script? Script { get; set; }

  public Guid DeviceId { get; set; }
  public Device? Device { get; set; }

  public string ExecutedByUserId { get; set; } = string.Empty;

  public DateTimeOffset StartedAt { get; set; }

  public DateTimeOffset? FinishedAt { get; set; }

  public ScriptStatus Status { get; set; }

  public string StdOut { get; set; } = string.Empty;

  public string StdErr { get; set; } = string.Empty;

  public int? ExitCode { get; set; }
}
