using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record TagResponseDto(
  Guid Id,
  string Name,
  TagType Type,
  IReadOnlyList<Guid> UserIds,
  IReadOnlyList<Guid> DeviceIds);