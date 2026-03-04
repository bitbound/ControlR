namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record CreateInstallerKeyResponseDto(
    Guid Id,
    Guid CreatorId,
    InstallerKeyType KeyType,
    string KeySecret,
    DateTimeOffset CreatedAt,
    uint? AllowedUses = null,
    DateTimeOffset? Expiration = null,
    string? FriendlyName = null);
