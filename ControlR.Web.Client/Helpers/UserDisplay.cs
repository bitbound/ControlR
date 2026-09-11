using UsersDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Users;

namespace ControlR.Web.Client.Helpers;

public static class UserDisplay
{
  public static string GetDisplayName(UsersDtos.UserResponseDto user)
  {
    var username = user.UserName ?? user.Email ?? user.Id.ToString();
    var displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? "—" : user.DisplayName;
    return $"{username}  (Display Name: {displayName}  |  User ID: {user.Id.ToString()[..8]}...)";
  }
}
