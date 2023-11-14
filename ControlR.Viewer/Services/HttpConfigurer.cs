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

    string GetDigitalSignature(PublicKeyDto keyDto);

    void UpdateClientAuthorizations(PublicKeyDto keyDto);
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
        client.BaseAddress = new Uri(AppConstants.ServerUri);

        if (_appState.IsAuthenticated)
        {
            var keyDto = new PublicKeyDto()
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

    public string GetDigitalSignature(PublicKeyDto keyDto)
    {
        var signedDto = _keyProvider.CreateSignedDto(keyDto, DtoType.PublicKey, _appState.UserKeys.PrivateKey);
        var dtoBytes = MessagePackSerializer.Serialize(signedDto);
        var base64Payload = Convert.ToBase64String(dtoBytes);
        return base64Payload;
    }

    public string GetDigitalSignature()
    {
        var publicKeyDto = new PublicKeyDto()
        {
            PublicKey = _settings.PublicKey,
            Username = _settings.Username
        };
        return GetDigitalSignature(publicKeyDto);
    }

    public void UpdateClientAuthorizations(PublicKeyDto keyDto)
    {
        var signature = GetDigitalSignature(keyDto);

        foreach (var client in _clients)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(AuthSchemes.DigitalSignature, signature);
        }
    }
}