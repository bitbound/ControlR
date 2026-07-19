using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

public record CreateInstallerKeyResponseDto(
  Guid Id,
  Guid CreatorId,
  InstallerKeyType KeyType,
  string KeySecret,
  DateTimeOffset CreatedAt,
  uint? AllowedUses = null,
  DateTimeOffset? Expiration = null,
  string? FriendlyName = null)
{
  public static CreateInstallerKeyResponseDto From(InternalDtos.CreateInstallerKeyResponseDto internalDto)
  {
    return new(
      internalDto.Id,
      internalDto.CreatorId,
      internalDto.KeyType,
      internalDto.KeySecret,
      internalDto.CreatedAt,
      internalDto.AllowedUses,
      internalDto.Expiration,
      internalDto.FriendlyName);
  }
}
