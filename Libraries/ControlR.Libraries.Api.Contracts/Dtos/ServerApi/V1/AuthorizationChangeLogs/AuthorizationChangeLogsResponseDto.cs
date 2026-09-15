namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.AuthorizationChangeLogs;

public class AuthorizationChangeLogsResponseDto
{
  public IReadOnlyList<AuthorizationChangeLogDto> Items { get; init; } = [];

  public int TotalItems { get; init; }
}