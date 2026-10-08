using System.Security.Claims;
using System.Text.Encodings.Web;
using ControlR.Libraries.Api.Contracts.Dtos.AgentApi;
using ControlR.Libraries.Shared.Services.Encryption;
using Microsoft.AspNetCore.Authentication;

namespace ControlR.Web.Server.Authn;

/// <summary>
/// Authenticates an installed agent by verifying a signature it produced with the Ed25519 key the
/// server already holds for that device. The signed payload names the request it authorizes, so a
/// captured header cannot be replayed against a different verb or path.
/// </summary>
public class AgentSignatureAuthenticationHandler(
  IDbContextFactory<AppDb> dbContextFactory,
  IEd25519KeyProvider keyProvider,
  UrlEncoder encoder,
  IOptionsMonitor<AgentSignatureAuthenticationSchemeOptions> options,
  ILoggerFactory logger) : AuthenticationHandler<AgentSignatureAuthenticationSchemeOptions>(options, logger, encoder)
{
  private static readonly TimeSpan _allowedClockSkew = TimeSpan.FromMinutes(5);

  protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    if (!Request.Headers.TryGetValue(Options.HeaderName, out var headerValues) ||
        !AgentSignatureHeader.TryDecode(headerValues.FirstOrDefault(), out var signedDto))
    {
      return AuthenticateResult.NoResult();
    }

    var attestation = signedDto.Dto;

    // The signature covers the request it authorizes, so a captured header cannot be replayed on
    // another verb or path.
    if (!string.Equals(attestation.Method, Request.Method, StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(attestation.PathAndQuery, $"{Request.Path}{Request.QueryString}", StringComparison.Ordinal))
    {
      return AuthenticateResult.Fail("The signed request does not match this request.");
    }

    if (!keyProvider.VerifyTimestamp(signedDto, _allowedClockSkew))
    {
      return AuthenticateResult.Fail("The signed request is outside the allowed clock skew.");
    }

    // The caller is not authenticated yet, so the tenant filter cannot apply and the device is
    // addressed by the id its own signature names.
    await using var db = await dbContextFactory.CreateDbContextAsync(Context.RequestAborted);
    var device = await db.Devices
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == attestation.DeviceId, Context.RequestAborted);

    if (device is null)
    {
      return AuthenticateResult.Fail("Unknown device.");
    }

    if (string.IsNullOrEmpty(device.PublicKey))
    {
      return AuthenticateResult.Fail("The device requires enrollment.");
    }

    var keyResult = keyProvider.ValidatePublicKeyBase64(device.PublicKey);
    if (!keyResult.IsSuccess)
    {
      Logger.LogWarning(
        "The stored public key for device {DeviceId} is not usable. Reason: {Reason}",
        device.Id,
        keyResult.Reason);
      return AuthenticateResult.Fail("The stored key for this device is not usable.");
    }

    if (!keyProvider.Verify(signedDto, keyResult.Value))
    {
      Logger.LogWarning("Signature verification failed. Device Id: {DeviceId}", device.Id);
      return AuthenticateResult.Fail("Signature verification failed.");
    }

    // No user id claim, so the pipeline's user-keyed middleware has nothing to resolve and passes
    // through. The tenant claim carries the device's own tenant.
    var claims = new List<Claim>
    {
      new(PrincipalClaimTypes.PrincipalType, PrincipalClaimValues.Agent),
      new(PrincipalClaimTypes.PrincipalId, device.Id.ToString()),
      new(UserClaimTypes.AuthenticationMethod, PrincipalClaimValues.AgentSignatureMethod),
      new(UserClaimTypes.TenantId, device.TenantId.ToString()),
      new(ClaimTypes.Name, device.Name),
    };

    var identity = new ClaimsIdentity(claims, Scheme.Name);
    var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
    return AuthenticateResult.Success(ticket);
  }
}
