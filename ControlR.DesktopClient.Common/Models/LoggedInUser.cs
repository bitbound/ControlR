namespace ControlR.DesktopClient.Common.Models;

public class LoggedInUser
{
    public string Username { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
