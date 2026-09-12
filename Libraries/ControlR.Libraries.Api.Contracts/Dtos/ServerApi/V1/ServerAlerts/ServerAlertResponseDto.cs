using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServerAlerts;

public record ServerAlertResponseDto(
  Guid? Id,
  string? Message,
  MessageSeverity Severity,
  bool IsDismissable,
  bool IsSticky,
  bool IsEnabled)
{
  [JsonIgnore]
  [IgnoreMember]
  [MemberNotNullWhen(true, nameof(Message))]
  public bool HasAlertSet => !string.IsNullOrWhiteSpace(Message);

  public static ServerAlertResponseDto Empty => new(
    Id: null,
    Message: null,
    Severity: default,
    IsDismissable: false,
    IsSticky: false,
    IsEnabled: false);
}
