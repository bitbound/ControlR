using MessagePack;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.Http.Headers;

namespace ControlR.Viewer.Services;

internal interface IHttpConfigurer
{
    void ConfigureClient(HttpClient client);

    HttpClient GetAuthorizedClient();

    string GetDigitalSignature();

    string GetDigitalSignature(IdentityDto keyDto);

    void UpdateClientAuthorizations(IdentityDto keyDto);
}

internal class HttpConfigurer(
    IHttpClientFactory _clientFactory,
    ISettings _settings,
    IKeyProvider _keyProvider,
    IAppState _appState,
    ILogger<HttpConfigurer> _logger) : IHttpConfigurer
{
    private static readonly ConcurrentBag<HttpClient> _clients = [];

    public void ConfigureClient(HttpClient client)
    {
        if (Uri.TryCreate(_settings.ServerUri, UriKind.Absolute, out var serverUri))
        {
            client.BaseAddress = serverUri;
        }
        else
        {
            _logger.LogError("Server URI in settings is invalid: {ServerUri}", _settings.ServerUri);
        }

        if (_appState.IsAuthenticated)
        {
            var keyDto = new IdentityDto()
            {
                Username = _settings.Username
            };

            var signature = GetDigitalSignature(keyDto);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(AuthSchemes.DigitalSignature, signature);
        }

        _clients.Add(client);
    }

    public HttpClient GetAuthorizedClient()
    {
        var client = _clientFactory.CreateClient();
        ConfigureClient(client);
        return client;
    }

    public string GetDigitalSignature(IdentityDto keyDto)
    {
        var signedDto = _keyProvider.CreateSignedDto(keyDto, DtoType.IdentityAttestation, _appState.PrivateKey);
        var dtoBytes = MessagePackSerializer.Serialize(signedDto);
        var base64Payload = Convert.ToBase64String(dtoBytes);
        return base64Payload;
    }

    public string GetDigitalSignature()
    {
        var identityDto = new IdentityDto()
        {
            Username = _settings.Username
        };
        return GetDigitalSignature(identityDto);
    }

    public void UpdateClientAuthorizations(IdentityDto keyDto)
    {
        var signature = GetDigitalSignature(keyDto);

        foreach (var client in _clients)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(AuthSchemes.DigitalSignature, signature);
        }
    }
}