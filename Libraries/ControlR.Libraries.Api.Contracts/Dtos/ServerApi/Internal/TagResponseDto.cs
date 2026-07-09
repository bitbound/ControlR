using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record TagResponseDto(
  Guid Id,
  string Name,
  TagType Type,
  IReadOnlyList<Guid> UserIds,
  IReadOnlyList<Guid> DeviceIds)
{
  public override string ToString() => Name;
}