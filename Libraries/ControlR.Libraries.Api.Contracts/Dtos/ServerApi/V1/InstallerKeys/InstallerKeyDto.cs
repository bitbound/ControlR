namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.InstallerKeys;

public record InstallerKeyDto(
  Guid Id,
  Guid CreatorId,
  string? CreatorName,
  InstallerKeyType KeyType,
  DateTimeOffset CreatedAt,
  uint? AllowedUses = null,
  DateTimeOffset? Expiration = null,
  string? FriendlyName = null,
  int UsageCount = 0);
