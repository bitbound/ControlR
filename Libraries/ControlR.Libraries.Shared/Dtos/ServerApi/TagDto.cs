using ControlR.Libraries.Shared.Enums;
using System.Collections.Immutable;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record TagDto(
  Guid Id,
  string Name, 
  TagType Type,
  ImmutableArray<Guid> UserIds,
  ImmutableArray<Guid> DeviceIds);