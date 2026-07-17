namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
public record RoleResponseDto(Guid Id, string Name, IReadOnlyList<Guid> UserIds);
