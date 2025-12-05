namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record CreateInstallerKeyRequestDto(
    InstallerKeyType KeyType,
    uint? AllowedUses = null,
    DateTimeOffset? Expiration = null,
    string? FriendlyName = null);