namespace ControlR.ApiClient.Auth;

public sealed record AuthSnapshot(
  string? PersonalAccessToken,
  string? BearerToken,
  DateTimeOffset? BearerTokenExpiresAt,
  string? RefreshToken);
