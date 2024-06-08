using ControlR.Server.Options;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Services.Http;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Server.Services;

public interface IIceServerProvider
{
    Task<IceServer[]> GetIceServers();
}

public class IceServerProvider(
    IMeteredApi _meteredApi,
    IHttpContextAccessor _httpContext,
    IOptionsMonitor<ApplicationOptions> _appOptions,
    ILogger<IceServerProvider> _logger) : IIceServerProvider
{
    public async Task<IceServer[]> GetIceServers()
    {
        try
        {
            if (_appOptions.CurrentValue.UseTwilio)
            {
                Guard.IsNotNullOrWhiteSpace(_appOptions.CurrentValue.TwilioSid);
                Guard.IsNotNullOrWhiteSpace(_appOptions.CurrentValue.TwilioSecret);
                TwilioClient.Init(_appOptions.CurrentValue.TwilioSid, _appOptions.CurrentValue.TwilioSecret);
                var token = TokenResource.Create();
                return token.IceServers
                    .Select(x => new IceServer()
                    {
                        Credential = x.Credential,
                        CredentialType = "password",
                        Urls = x.Urls.ToString(),
                        Username = x.Username
                    })
                    .ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting Metered ICE servers.");
        }

        try
        {
            if (_appOptions.CurrentValue.UseMetered)
            {
                Guard.IsNotNullOrWhiteSpace(_appOptions.CurrentValue.MeteredApiKey);
                return await _meteredApi.GetIceServers(_appOptions.CurrentValue.MeteredApiKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting Metered ICE servers.");
        }

        try
        {
            if (_appOptions.CurrentValue.UseCoTurn)
            {
                var coturnUser = _appOptions.CurrentValue.CoTurnUsername;
                var coturnSecret = _appOptions.CurrentValue.CoTurnSecret;

                Guard.IsNotNullOrWhiteSpace(coturnUser);
                Guard.IsNotNullOrWhiteSpace(coturnSecret);

                var (user, password) = GenerateTurnPassword(coturnSecret, coturnUser);

                if (_httpContext.HttpContext is null)
                {
                    _logger.LogError("HttpContext is null.  Unable to provide CoTurn ICE servers.");
                    return [];
                }

                var host = _appOptions.CurrentValue.CoTurnHost is { } coturnHost
                    ? coturnHost
                    : _httpContext.HttpContext.Request.Host.Host;

                var iceServer = new IceServer()
                {
                    Credential = password,
                    CredentialType = "password",
                    Username = user,
                    Urls = $"turn:{host}:{_appOptions.CurrentValue.CoTurnPort}"
                };
                return [iceServer];
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting coTURN ICE servers.");
        }

        try
        {
            if (_appOptions.CurrentValue.UseStaticIceServers &&
               _appOptions.CurrentValue.IceServers.Count > 0)
            {
                return [.._appOptions.CurrentValue.IceServers];
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting ICE servers.");
        }

        _logger.LogWarning("No ICE server provider configured.");
        return [];
    }

    private static (string tempUser, string tempPassword) GenerateTurnPassword(string secret, string username = "")
    {
        var expiration = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds();
        var tempUser = !string.IsNullOrWhiteSpace(username) ? 
            $"{expiration}:{username}" :
            $"{expiration}";

        var key = Encoding.ASCII.GetBytes(secret);
        using var hmacsha1 = new HMACSHA1(key);

        var buffer = Encoding.ASCII.GetBytes(tempUser);
        var hashValue = hmacsha1.ComputeHash(buffer);
        var tempPassword = Convert.ToBase64String(hashValue);

        return (tempUser, tempPassword);
    }
}