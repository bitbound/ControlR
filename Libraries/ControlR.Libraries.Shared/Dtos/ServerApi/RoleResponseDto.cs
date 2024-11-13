using System.Collections.Immutable;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;
public record RoleResponseDto(Guid Id, string Name, ImmutableList<Guid> UserIds) : IHasPrimaryKey;
