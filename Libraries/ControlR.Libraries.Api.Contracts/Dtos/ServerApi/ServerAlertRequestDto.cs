using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record ServerAlertRequestDto(
  string Message,
  MessageSeverity Severity,
  bool IsDismissable,
  bool IsSticky,
  bool IsEnabled);
