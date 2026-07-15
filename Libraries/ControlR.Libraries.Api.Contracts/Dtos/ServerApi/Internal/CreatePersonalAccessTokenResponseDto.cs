using System;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record CreatePersonalAccessTokenResponseDto(
  PersonalAccessTokenResponseDto PersonalAccessToken,
  string PlainTextToken);
