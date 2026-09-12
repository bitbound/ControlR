namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.EffectivePermissions;

/// <summary>
/// Result of evaluating a single permission for a user.
/// </summary>
public record EffectivePermissionQueryResponseDto(
  bool IsAllowed,
  string? DenyReason);