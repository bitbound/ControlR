namespace ControlR.ApiClient.Auth;

public sealed record AuthState(
  long BearerStateVersion,
  AuthSnapshot Snapshot);
