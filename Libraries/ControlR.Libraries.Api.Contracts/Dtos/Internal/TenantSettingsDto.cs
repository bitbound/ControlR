namespace ControlR.Libraries.Api.Contracts.Dtos.Internal;

public sealed record TenantSettingsDto(
  bool? AppendInstanceId,
  string? InstanceId,
  bool? NotifyUserOnSessionStart);