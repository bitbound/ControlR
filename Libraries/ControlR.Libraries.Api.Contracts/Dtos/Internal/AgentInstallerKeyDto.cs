namespace ControlR.Libraries.Api.Contracts.Dtos.Internal;

public record AgentInstallerKeyDto(
    Guid Id,
    Guid CreatorId,
    string? CreatorName,
    InstallerKeyType KeyType,
    DateTimeOffset CreatedAt,
    uint? AllowedUses = null,
    DateTimeOffset? Expiration = null,
    string? FriendlyName = null,
    int UsageCount = 0);
