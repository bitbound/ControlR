using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record ScriptDto(
  Guid Id,
  string Name,
  string Description,
  string CodeContent,
  ShellType ShellType,
  int TimeoutSeconds,
  DateTimeOffset CreatedAt);
