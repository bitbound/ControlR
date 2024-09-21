using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace ControlR.Web.Client.Extensions;

public static class ClaimsPrincipalExtensions
{
  public static bool IsAdministrator(this ClaimsPrincipal user)
  {
    return
      user.TryGetClaim(ClaimNames.IsAdministrator, out var claimValue) &&
      bool.TryParse(claimValue, out var isAdmin) &&
      isAdmin;
  }

  public static bool IsAuthenticated(this ClaimsPrincipal user)
  {
    return user.Identity?.IsAuthenticated ?? false;
  }

  public static bool TryGetClaim(
    this ClaimsPrincipal user,
    string claimType,
    [NotNullWhen(true)] out string? claimValue)
  {
    claimValue = user.Claims.FirstOrDefault(x => x.Type == claimType)?.Value ?? string.Empty;
    return !string.IsNullOrWhiteSpace(claimValue);
  }
}