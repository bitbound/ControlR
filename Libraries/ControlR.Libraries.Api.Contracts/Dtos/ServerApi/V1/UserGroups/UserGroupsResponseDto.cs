namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserGroups;

public class UserGroupsResponseDto
{
  public IReadOnlyList<UserGroupDto> Items { get; set; } = [];
}