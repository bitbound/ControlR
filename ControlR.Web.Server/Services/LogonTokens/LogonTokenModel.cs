using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.Web.Server.Services.LogonTokens;

public class LogonTokenModel
{
  public required DateTimeOffset CreatedAt { get; set; }
  public required Guid DeviceId { get; set; }
  public required DateTimeOffset ExpiresAt { get; set; }
  public bool IsConsumed { get; set; }
  public required LogonTokenKind Kind { get; set; }
  public required Guid TenantId { get; set; }
  public required string Token { get; set; }
  public Guid? UserId { get; set; }
}
