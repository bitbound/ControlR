namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PersonalAccessTokens;

public record CreatePersonalAccessTokenResponseDto(
  PersonalAccessTokenResponseDto PersonalAccessToken,
  string PlainTextToken);
