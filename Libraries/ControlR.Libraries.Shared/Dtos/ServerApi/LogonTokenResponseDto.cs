namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public class LogonTokenResponseDto
{
  public required Uri DeviceAccessUrl { get; set; }
  public DateTimeOffset ExpiresAt { get; set; }
  public required string Token { get; set; }
}
