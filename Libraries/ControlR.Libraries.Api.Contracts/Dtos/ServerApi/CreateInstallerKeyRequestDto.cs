namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record CreateInstallerKeyRequestDto(
    InstallerKeyType KeyType,
    uint? AllowedUses = null,
    DateTimeOffset? Expiration = null,
    string? FriendlyName = null);