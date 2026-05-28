namespace ControlR.ApiClient.Auth;

public sealed record BearerRefreshContext(
  long ExpectedBearerStateVersion,
  string RefreshToken);
