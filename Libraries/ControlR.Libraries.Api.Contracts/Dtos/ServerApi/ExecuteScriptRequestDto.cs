using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record ExecuteScriptRequestDto(
  Guid[] DeviceIds,
  string? AdHocScriptContent,
  ShellType? ShellType,
  ScriptRunAs RunAs = ScriptRunAs.System);
