namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PersonalAccessTokens;

public class PersonalAccessTokensResponseDto
{
  public IReadOnlyList<PersonalAccessTokenResponseDto> Items { get; set; } = [];
}
