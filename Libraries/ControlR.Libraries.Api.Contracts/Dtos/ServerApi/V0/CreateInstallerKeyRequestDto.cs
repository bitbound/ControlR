using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public record CreateInstallerKeyRequestDto(
  Guid TenantId,
  Guid CreatorId,
  CreatorKind CreatorKind,
  InstallerKeyType KeyType,
  string? FriendlyName = null,
  uint? AllowedUses = null,
  DateTimeOffset? Expiration = null);
