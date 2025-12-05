namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record AgentInstallerKeyDto(
    Guid Id,
    Guid CreatorId,
    InstallerKeyType KeyType,
    DateTimeOffset CreatedAt,
    uint? AllowedUses = null,
    DateTimeOffset? Expiration = null,
    string? FriendlyName = null,
    List<AgentInstallerKeyUsageDto>? Usages = null)
{
  public List<AgentInstallerKeyUsageDto> Usages { get; init; } = Usages ?? [];
};
