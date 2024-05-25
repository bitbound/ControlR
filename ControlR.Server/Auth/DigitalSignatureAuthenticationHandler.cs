﻿using ControlR.Server.Options;
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
    IDigitalSignatureAuthenticator _authenticator,
    IOptionsMonitor<AuthenticationSchemeOptions> _options,
    ILoggerFactory _loggerFactory,
    ILogger<DigitalSignatureAuthenticationHandler> _logger) : AuthenticationHandler<AuthenticationSchemeOptions>(_options, _loggerFactory, _encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        using var _ = _logger.BeginScope(nameof(HandleAuthenticateAsync));
        var authHeader = Context.Request.Headers.Authorization.FirstOrDefault(x =>
            x?.StartsWith(AuthSchemes.DigitalSignature) == true);

        return await _authenticator.Authenticate(authHeader);
    }
}