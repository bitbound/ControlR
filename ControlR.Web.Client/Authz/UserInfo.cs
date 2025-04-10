namespace ControlR.Web.Client.Authz;

// Add properties to this class and update the server and client AuthenticationStateProviders
// to expose more information about the authenticated user to the client.
public class UserInfo
{
  public List<UserClaim> Claims { get; set; } = [];
  public required string Email { get; set; }
  public List<string> Roles { get; set; } = [];
  public required string UserId { get; set; }
}