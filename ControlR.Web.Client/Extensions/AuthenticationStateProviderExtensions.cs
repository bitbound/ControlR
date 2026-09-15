using Microsoft.AspNetCore.Components.Authorization;

namespace ControlR.Web.Client.Extensions;

public static class AuthenticationStateProviderExtensions
{
  /// <summary>
  /// Reads the tenant from the current authentication state without reporting a failure, for callers
  /// that already have their own fallback.
  /// </summary>
  public static async Task<Guid?> GetTenantId(
    this AuthenticationStateProvider authState)
  {
    var state = await authState.GetAuthenticationStateAsync();
    return state.User.TryGetTenantId(out var tenantId) ? tenantId : null;
  }

  /// <summary>
  /// Reads the tenant from the current authentication state, notifying
  /// <paramref name="snackbar"/> when there is none.
  /// </summary>
  public static async Task<Guid?> GetTenantId(
    this AuthenticationStateProvider authState,
    ISnackbar snackbar)
  {
    var state = await authState.GetAuthenticationStateAsync();
    return state.User.TryGetTenantId(snackbar, out var tenantId) ? tenantId : null;
  }

  public static async Task<bool> IsAuthenticated(this AuthenticationStateProvider provider)
  {
    var state = await provider.GetAuthenticationStateAsync();
    return state.User.Identity?.IsAuthenticated ?? false;
  }
}
