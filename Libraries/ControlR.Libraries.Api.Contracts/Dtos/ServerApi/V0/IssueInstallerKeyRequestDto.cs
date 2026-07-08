using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public record IssueInstallerKeyRequestDto(
  Guid TenantId,
  Guid CreatorId,
  InstallerKeyType KeyType,
  uint? AllowedUses = null,
  DateTimeOffset? Expiration = null,
  string? FriendlyName = null);
