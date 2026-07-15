using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public record CreateInstallerKeyRequestDto(
  [property: Required]
  Guid TenantId,
  [property: Required]
  Guid CreatorId,
  CreatorKind CreatorKind,
  InstallerKeyType KeyType,
  string? FriendlyName = null,
  uint? AllowedUses = null,
  DateTimeOffset? Expiration = null);
