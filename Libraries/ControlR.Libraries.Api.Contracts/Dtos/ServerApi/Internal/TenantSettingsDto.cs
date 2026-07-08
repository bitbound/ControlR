namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public sealed record TenantSettingsDto(
  bool? AppendInstanceId,
  string? InstanceId,
  bool? NotifyUserOnSessionStart);