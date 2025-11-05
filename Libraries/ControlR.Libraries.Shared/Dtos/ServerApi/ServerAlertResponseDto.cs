using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record ServerAlertResponseDto(
  Guid Id,
  string Message,
  MessageSeverity Severity,
  bool IsDismissable,
  bool IsSticky,
  bool IsEnabled) : EntityBaseRecordDto(Id);
