using System.Collections.Concurrent;
using System.Net.Http.Headers;
using MessagePack;

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
  IHttpClientFactory clientFactory,
  ISettings settings,
  IKeyProvider keyProvider,
  IAppState appState) : IHttpConfigurer
{
  private static readonly ConcurrentBag<HttpClient> _clients = [];

  public void ConfigureClient(HttpClient client)
  {
    client.BaseAddress = settings.ServerUri;

    if (appState.IsAuthenticated)
    {
      var keyDto = new IdentityDto
      {
        Username = settings.Username
      };

      var signature = GetDigitalSignature(keyDto);
      client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue(AuthSchemes.DigitalSignature, signature);
    }

    _clients.Add(client);
  }

  public HttpClient GetAuthorizedClient()
  {
    var client = clientFactory.CreateClient();
    ConfigureClient(client);
    return client;
  }

  public string GetDigitalSignature(IdentityDto keyDto)
  {
    var signedDto = keyProvider.CreateSignedDto(keyDto, DtoType.IdentityAttestation, appState.PrivateKey);
    var dtoBytes = MessagePackSerializer.Serialize(signedDto);
    var base64Payload = Convert.ToBase64String(dtoBytes);
    return base64Payload;
  }

  public string GetDigitalSignature()
  {
    var identityDto = new IdentityDto
    {
      Username = settings.Username
    };
    return GetDigitalSignature(identityDto);
  }

  public void UpdateClientAuthorizations(IdentityDto keyDto)
  {
    var signature = GetDigitalSignature(keyDto);

    foreach (var client in _clients)
    {
      client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue(AuthSchemes.DigitalSignature, signature);
    }
  }
}