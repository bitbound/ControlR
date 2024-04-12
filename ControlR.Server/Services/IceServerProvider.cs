using ControlR.Server.Options;
using ControlR.Shared.Models;
using ControlR.Shared.Primitives;
using ControlR.Shared.Services.Http;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace ControlR.Server.Services;

public interface IIceServerProvider
{
    Task<IceServer[]> GetIceServers();
}

public class IceServerProvider(
    IOptionsMonitor<ApplicationOptions> _appOptions,
    IMeteredApi _meteredApi,
    ILogger<IceServerProvider> _logger) : IIceServerProvider
{
    public async Task<IceServer[]> GetIceServers()
    {
        var iceServers = new List<IceServer>();

        try
        {

            if (_appOptions.CurrentValue.UseStaticIceServers &&
                _appOptions.CurrentValue.IceServers.Count > 0)
            {
                iceServers.AddRange(_appOptions.CurrentValue.IceServers);
            }

            if (_appOptions.CurrentValue.UseMetered &&
                !string.IsNullOrWhiteSpace(_appOptions.CurrentValue.MeteredApiKey))
            {
                var servers = await _meteredApi.GetIceServers(_appOptions.CurrentValue.MeteredApiKey);
                iceServers.AddRange(servers);
            }

            if (_appOptions.CurrentValue.UseCoTurn &&
                !string.IsNullOrWhiteSpace(_appOptions.CurrentValue.CoTurnSecret))
            {
                // TODO: Get coTURN creds.
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting ICE servers.");
        }

        return [..iceServers];
    }

    private string GenerateTurnPassword(string secret, string username)
    {
        var expiration = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds();
        var tempUser = $"{expiration}:{username}";

        var key = Encoding.ASCII.GetBytes(secret);
        using var hmacsha1 = new HMACSHA1(key);

        var buffer = Encoding.ASCII.GetBytes(tempUser);
        var hashValue = hmacsha1.ComputeHash(buffer);
        return Convert.ToBase64String(hashValue);
    }
}