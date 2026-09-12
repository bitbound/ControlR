namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.TenantSettings;

/// <summary>
/// The tenant's settings as a typed aggregate. A null member means the setting is unset, which
/// is distinct from a set value that happens to be the definition's default.
/// </summary>
public sealed record TenantSettingsDto(
  bool? AppendInstanceId,
  string? InstanceId,
  bool? NotifyUserOnSessionStart);
