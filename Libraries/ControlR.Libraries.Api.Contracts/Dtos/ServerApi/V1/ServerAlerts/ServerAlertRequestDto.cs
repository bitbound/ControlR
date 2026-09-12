namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServerAlerts;

public record ServerAlertRequestDto(
  string Message,
  MessageSeverity Severity,
  bool IsDismissable,
  bool IsSticky,
  bool IsEnabled);
