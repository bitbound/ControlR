using MessagePack;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using ControlR.Server.Options;
using ControlR.Libraries.Shared;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Services;

namespace ControlR.Server.Auth;

public interface IDigitalSignatureAuthenticator
{
    Task<AuthenticateResult> Authenticate(string? authHeader);
}
public class DigitalSignatureAuthenticator(
    IServiceScopeFactory _scopeFactory,
    IOptionsMonitor<ApplicationOptions> _appOptions,
    ILogger<DigitalSignatureAuthenticator> _logger) : IDigitalSignatureAuthenticator
{
    public Task<AuthenticateResult> Authenticate(string? authHeader)
    {

        if (string.IsNullOrWhiteSpace(authHeader))
        {
            return AuthenticateResult
                .Fail($"{AuthSchemes.DigitalSignature} authorization is missing.")
                .AsTaskResult();
        }

        try
        {
            if (!TryGetSignedPayload(authHeader, out var signedDto))
            {
                return AuthenticateResult
                    .Fail("Failed to parse authorization header.")
                    .AsTaskResult();
            }

            return VerifySignature(signedDto).AsTaskResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse header {authHeader}.", authHeader);
            return AuthenticateResult.Fail("An error occurred on the server.").AsTaskResult();
        }
    }

    private bool TryGetSignedPayload(string header, out SignedPayloadDto signedPayload)
    {
        var base64Token = header.Split(" ", 2).Skip(1).First();
        var payloadBytes = Convert.FromBase64String(base64Token);
        signedPayload = MessagePackSerializer.Deserialize<SignedPayloadDto>(payloadBytes);

        if (signedPayload.DtoType != DtoType.IdentityAttestation)
        {
            _logger.LogWarning("Unexpected DTO type of {type}.", signedPayload.DtoType);
            return false;
        }

        return true;
    }

    private AuthenticateResult VerifySignature(SignedPayloadDto signedDto)
    {
        using var scope = _scopeFactory.CreateScope();
        var keyProvider = scope.ServiceProvider.GetRequiredService<IKeyProvider>();

        var account = MessagePackSerializer.Deserialize<IdentityDto>(signedDto.Payload);

        var result = keyProvider.Verify(signedDto);

        if (!result)
        {
            return AuthenticateResult.Fail("Digital signature verification failed.");
        }

        var claims = new List<Claim>
        {
            new(ClaimNames.PublicKey, signedDto.PublicKeyBase64),
            new(ClaimNames.Username, account.Username),
        };

        if (_appOptions.CurrentValue.AdminPublicKeys.Contains(signedDto.PublicKeyBase64))
        {
            claims.Add(new(ClaimNames.IsAdministrator, "true"));
        }
        else if (_appOptions.CurrentValue.EnableRestrictedUserAccess &&
                !_appOptions.CurrentValue.AuthorizedUserPublicKeys.Contains(signedDto.PublicKeyBase64))
        {
            return AuthenticateResult.Fail("Access to this server is restricted.");
        }

        var identity = new ClaimsIdentity(claims, AuthSchemes.DigitalSignature);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, AuthSchemes.DigitalSignature));
    }
}
