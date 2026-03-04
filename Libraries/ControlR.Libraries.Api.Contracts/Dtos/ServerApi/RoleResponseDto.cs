namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
public record RoleResponseDto(Guid Id, string Name, IReadOnlyList<Guid> UserIds);
