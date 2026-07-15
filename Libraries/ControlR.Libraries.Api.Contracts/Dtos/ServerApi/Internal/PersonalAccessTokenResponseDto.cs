using System;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record PersonalAccessTokenResponseDto(
  Guid Id,
  string Name,
  DateTimeOffset CreatedAt,
  DateTimeOffset? LastUsed);
