using System.Security.Claims;

namespace ControlR.Web.Client.Authn;

public class ControlrClaimsIdentity(
  UserInfo userInfo, 
  IEnumerable<Claim> claims, 
  string authenticationType) : ClaimsIdentity(claims, authenticationType)
{
  private readonly UserInfo _userInfo = userInfo;
  public override bool IsAuthenticated => _userInfo.IsAuthenticated;
}
