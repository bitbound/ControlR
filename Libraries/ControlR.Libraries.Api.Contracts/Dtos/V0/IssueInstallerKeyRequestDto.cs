using ControlR.Libraries.Api.Contracts.Dtos.Internal;

namespace ControlR.Libraries.Api.Contracts.Dtos.V0;

public record IssueInstallerKeyRequestDto(
  Guid TenantId,
  Guid CreatorId,
  InstallerKeyType KeyType,
  uint? AllowedUses = null,
  DateTimeOffset? Expiration = null,
  string? FriendlyName = null);
