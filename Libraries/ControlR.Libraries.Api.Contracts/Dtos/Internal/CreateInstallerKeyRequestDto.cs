namespace ControlR.Libraries.Api.Contracts.Dtos.Internal;

public record CreateInstallerKeyRequestDto(
    InstallerKeyType KeyType,
    uint? AllowedUses = null,
    DateTimeOffset? Expiration = null,
    string? FriendlyName = null);