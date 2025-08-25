namespace ControlR.Web.Server.Services.LogonTokens;

public class LogonTokenModel
{
  public required string Token { get; set; }
  public required Guid DeviceId { get; set; }
  public required DateTimeOffset ExpiresAt { get; set; }
  public required Guid UserId { get; set; }
  public required Guid TenantId { get; set; }
  public required DateTimeOffset CreatedAt { get; set; }
  public bool IsConsumed { get; set; }
}
