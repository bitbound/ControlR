namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public class LogonTokenRequestDto
{
  public required Guid DeviceId { get; set; }
  public int ExpirationMinutes { get; set; } = 15; // Default 15 minutes
  public string? UserIdentifier { get; set; } // Optional user context
  public string? DisplayName { get; set; }
  public string? Email { get; set; }
}
