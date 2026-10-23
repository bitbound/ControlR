namespace ControlR.ApiClient.Auth;

public sealed record InteractiveLoginResult(
  InteractiveLoginStatus Status,
  string? Message = null);