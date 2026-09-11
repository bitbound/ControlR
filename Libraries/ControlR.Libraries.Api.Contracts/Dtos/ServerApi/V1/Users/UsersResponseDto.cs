namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Users;

public class UsersResponseDto
{
  public IReadOnlyList<UserResponseDto> Items { get; set; } = [];
}
