using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record ScriptExecutionDto(
  Guid Id,
  Guid? ScriptId,
  string ScriptName,
  Guid DeviceId,
  string DeviceName,
  string ExecutedByUserId,
  DateTimeOffset StartedAt,
  DateTimeOffset? FinishedAt,
  ScriptStatus Status,
  string StdOut,
  string StdErr,
  int? ExitCode);
