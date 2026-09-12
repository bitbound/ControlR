using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PersonalAccessTokens;

public record CreatePersonalAccessTokenRequestDto(
  [property: Required]
  [property: StringLength(256, MinimumLength = 1)]
  string Name,
  PersonalAccessTokenPermissionMode PermissionMode = PersonalAccessTokenPermissionMode.Restricted,
  IReadOnlyList<CredentialScopeDto>? Scopes = null);
