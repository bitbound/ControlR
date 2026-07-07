namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record IssueInstallerKeyRequestDto(
  Guid TenantId,
  Guid CreatorId,
  InstallerKeyType KeyType,
  uint? AllowedUses = null,
  DateTimeOffset? Expiration = null,
  string? FriendlyName = null);
