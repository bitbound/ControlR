using ControlR.Server.Options;
using ControlR.Shared;
using ControlR.Shared.Dtos;
using ControlR.Shared.Extensions;
using ControlR.Shared.Services;
using MessagePack;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace ControlR.Server.Auth;

public class DigitalSignatureAuthenticationHandler(
    UrlEncoder _encoder,
    IOptionsMonitor<AuthenticationSchemeOptions> _options,
    ILoggerFactory _loggerFactory,
    IServiceScopeFactory _scopeFactory,
    IOptionsMonitor<AuthorizationOptions> _authOptions,
    ILogger<DigitalSignatureAuthenticationHandler> _logger) : AuthenticationHandler<AuthenticationSchemeOptions>(_options, _loggerFactory, _encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        using var _ = _logger.BeginScope(nameof(HandleAuthenticateAsync));
        _logger.LogInformation("Start auth handler.");
        var authHeader = Context.Request.Headers.Authorization.FirstOrDefault(x =>
            x?.StartsWith(AuthSchemes.DigitalSignature) == true);

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
        var publicKey = account.PublicKey;

        if (publicKey.Length == 0)
        {
            return AuthenticateResult.Fail("No public key found in the payload.");
        }

        if (!publicKey.SequenceEqual(signedDto.PublicKey))
        {
            return AuthenticateResult.Fail("Public key of the signed DTO does not match the payload.");
        }

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

        if (_authOptions.CurrentValue.AdminPublicKeys.Contains(signedDto.PublicKeyBase64))
        {
            claims.Add(new(ClaimNames.IsAdministrator, "true"));
        }
        else if (_authOptions.CurrentValue.EnableRestrictedUserAccess &&
                !_authOptions.CurrentValue.AuthorizedUserPublicKeys.Contains(signedDto.PublicKeyBase64))
        {
            return AuthenticateResult.Fail("Access to this server is restricted.");
        }

        var identity = new ClaimsIdentity(claims, AuthSchemes.DigitalSignature);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, AuthSchemes.DigitalSignature));
    }
}