using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PersonalAccessTokens;

public record UpdatePersonalAccessTokenRequestDto(
  [property: Required]
  [property: StringLength(256, MinimumLength = 1)]
  string Name);
