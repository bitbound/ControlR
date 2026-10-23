namespace ControlR.ApiClient.Auth;

public sealed record InteractiveSignInRequest
{
  public required string Email { get; init; }
  public required string Password { get; init; }
  public string? TwoFactorCode { get; init; }
  public string? RecoveryCode { get; init; }
}