using System.Security.Claims;

namespace ControlR.Web.Client;

// Add properties to this class and update the server and client AuthenticationStateProviders
// to expose more information about the authenticated user to the client.
public class UserInfo
{
  public required string UserId { get; set; }
  public required string Email { get; set; }
  public IList<string> Roles { get; set; } = [];
  public IList<Claim> Claims { get; set; } = [];
}