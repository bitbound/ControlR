namespace ControlR.Web.Server.Extensions;

public static class UserManagerExtensions
{
  public static async Task UpdateLastLogin(this UserManager<AppUser> userManager, AppUser user)
  {
    user.LastLogin = DateTimeOffset.UtcNow;
    await userManager.UpdateAsync(user);
  }
}
