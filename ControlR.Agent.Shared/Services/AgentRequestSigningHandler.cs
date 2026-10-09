using ControlR.Libraries.Api.Contracts.Dtos.AgentApi;
using ControlR.Libraries.Shared.Services.Encryption;

namespace ControlR.Agent.Shared.Services;

/// <summary>
/// Signs each outgoing API request with this device's Ed25519 key so the server can authenticate the
/// device without a shared secret. The signature covers the request's verb and path, so a header
/// captured from one call cannot be replayed against another.
/// </summary>
public class AgentRequestSigningHandler(
  IOptionsAccessor optionsAccessor,
  IEd25519KeyProvider keyProvider) : DelegatingHandler
{
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    var privateKeyBase64 = optionsAccessor.PrivateKey;

    // An install that has not enrolled yet has no key to sign with, and the endpoints it needs at
    // that point are anonymous, so it simply presents no signature.
    if (!string.IsNullOrWhiteSpace(privateKeyBase64) && request.RequestUri is { } requestUri)
    {
      var attestation = new AgentRequestAttestationDto(
        optionsAccessor.DeviceId,
        request.Method.Method,
        GetPathAndQuery(requestUri));

      var signedDto = keyProvider.Sign(
        attestation,
        Convert.FromBase64String(privateKeyBase64),
        keyProvider.DerivePublicKeyBase64(privateKeyBase64));

      request.Headers.TryAddWithoutValidation(AgentSignatureHeader.Name, AgentSignatureHeader.Encode(signedDto));
    }

    return await base.SendAsync(request, cancellationToken);
  }

  private static string GetPathAndQuery(Uri requestUri)
  {
    // The server rebuilds this from the request line, which never carries a scheme or authority.
    return requestUri.IsAbsoluteUri
      ? $"{requestUri.AbsolutePath}{requestUri.Query}"
      : requestUri.OriginalString;
  }
}
