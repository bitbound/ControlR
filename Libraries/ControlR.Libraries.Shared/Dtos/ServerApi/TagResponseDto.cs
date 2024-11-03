using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record TagResponseDto(
  Guid Id,
  string Name,
  TagType Type,
  IReadOnlyList<Guid> UserIds,
  IReadOnlyList<Guid> DeviceIds) : IHasPrimaryKey;