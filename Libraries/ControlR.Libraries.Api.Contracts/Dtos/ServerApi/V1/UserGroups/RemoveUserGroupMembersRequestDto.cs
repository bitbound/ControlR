using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserGroups;

public record RemoveUserGroupMembersRequestDto(
  [property: Required]
  IReadOnlyList<Guid> UserIds);