using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record ScriptCreateRequestDto(
  string Name,
  string Description,
  string CodeContent,
  ShellType ShellType,
  int TimeoutSeconds);
