namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public class LogonTokenRequestDto
{
  public required Guid DeviceId { get; set; }
  public int ExpirationMinutes { get; set; } = 15; // Default 15 minutes
  public required Guid UserId { get; set; } // Required user ID
}
