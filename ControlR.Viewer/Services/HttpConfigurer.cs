using ControlR.Shared;
using ControlR.Shared.Dtos;
using ControlR.Shared.Services;
using MessagePack;
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
    IAppState _appState) : IHttpConfigurer
{
    private static readonly ConcurrentBag<HttpClient> _clients = [];

    public void ConfigureClient(HttpClient client)
    {
        client.BaseAddress = new Uri(_settings.ServerUri);

        if (_appState.IsAuthenticated)
        {
            var keyDto = new IdentityDto()
            {
                PublicKey = _settings.PublicKey,
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
        var signedDto = _keyProvider.CreateSignedDto(keyDto, DtoType.IdentityAttestation, _settings.UserKeys.PrivateKey);
        var dtoBytes = MessagePackSerializer.Serialize(signedDto);
        var base64Payload = Convert.ToBase64String(dtoBytes);
        return base64Payload;
    }

    public string GetDigitalSignature()
    {
        var identityDto = new IdentityDto()
        {
            PublicKey = _settings.PublicKey,
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