namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record ServerRenameInstallerKeyRequestDto(
  Guid TenantId,
  Guid UserId,
  bool IsTenantAdmin,
  Guid Id,
  string FriendlyName);