namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Users;

public record CreateUserRequestDto(
  string UserName,
  string? Email,
  string? Password,
  IReadOnlyList<string>? PresetNames);
