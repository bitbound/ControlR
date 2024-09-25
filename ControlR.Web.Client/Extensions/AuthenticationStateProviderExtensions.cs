using Microsoft.AspNetCore.Components.Authorization;

namespace ControlR.Web.Client.Extensions;

public static class AuthenticationStateProviderExtensions
{
  public static async Task<bool> IsAuthenticated(this AuthenticationStateProvider provider)
  {
    var state = await provider.GetAuthenticationStateAsync();
    return state.User.Identity?.IsAuthenticated ?? false;
  }
}
