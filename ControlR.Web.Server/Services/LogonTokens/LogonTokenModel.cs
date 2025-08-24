namespace ControlR.Web.Server.Services.LogonTokens;

public class LogonTokenModel
{
  public required string Token { get; set; }
  public required Guid DeviceId { get; set; }
  public required DateTimeOffset ExpiresAt { get; set; }
  public string? UserIdentifier { get; set; }
  public string? DisplayName { get; set; }
  public string? Email { get; set; }
  public required Guid TenantId { get; set; }
  public required DateTimeOffset CreatedAt { get; set; }
  public bool IsConsumed { get; set; }
}
