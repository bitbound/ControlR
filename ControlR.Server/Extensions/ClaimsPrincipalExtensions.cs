using ControlR.Server.Auth;
using System.Security.Claims;

namespace ControlR.Server.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static bool IsAdministrator(this ClaimsPrincipal user)
    {
        return
            TryGetClaim(user, ClaimNames.IsAdministrator, out var claimValue) &&
            bool.TryParse(claimValue, out var isAdmin) &&
            isAdmin;
    }
    public static bool TryGetClaim(this ClaimsPrincipal user, string claimType, out string claimValue)
    {
        claimValue = user.Claims.FirstOrDefault(x => x.Type == claimType)?.Value ?? string.Empty;
        return !string.IsNullOrWhiteSpace(claimValue);
    }

    public static bool TryGetPublicKey(this ClaimsPrincipal user, out string publicKey)
    {
        if (TryGetClaim(user, ClaimNames.PublicKey, out publicKey))
        {
            return true;
        }

        return false;
    }

}