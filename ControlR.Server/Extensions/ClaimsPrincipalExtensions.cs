using ControlR.Server.Auth;
using System.Security.Claims;

namespace ControlR.Server.Extensions;

public static class ClaimsPrincipalExtensions
{
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