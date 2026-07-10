using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public record CreateInstallerKeyResponseDto(
    Guid Id,
    Guid CreatorId,
    InstallerKeyType KeyType,
    string KeySecret,
    DateTimeOffset CreatedAt,
    uint? AllowedUses = null,
    DateTimeOffset? Expiration = null,
    string? FriendlyName = null);
