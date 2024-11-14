using System.Collections.Immutable;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;
public record RoleResponseDto(Guid Id, string Name, IReadOnlyList<Guid> UserIds) : IHasPrimaryKey;
