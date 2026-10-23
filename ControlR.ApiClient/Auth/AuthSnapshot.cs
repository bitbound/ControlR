namespace ControlR.ApiClient.Auth;

/// <summary>
/// A point-in-time copy of a client's credentials, used for persistence and restore.
/// </summary>
/// <param name="PersonalAccessToken">The configured personal access token, sent as <c>x-personal-token</c>.</param>
/// <param name="BearerToken">The interactive access token, sent as <c>Authorization: Bearer</c>.</param>
/// <param name="BearerTokenExpiresAt">When <paramref name="BearerToken"/> stops being usable.</param>
/// <param name="RefreshToken">The token that renews <paramref name="BearerToken"/>.</param>
/// <param name="ServiceAccountApiKey">
/// The service account credential, sent as <c>x-api-key</c>. Trailing and optional so that existing
/// four-argument constructions keep compiling.
/// </param>
public sealed record AuthSnapshot(
  string? PersonalAccessToken,
  string? BearerToken,
  DateTimeOffset? BearerTokenExpiresAt,
  string? RefreshToken,
  string? ServiceAccountApiKey = null);
