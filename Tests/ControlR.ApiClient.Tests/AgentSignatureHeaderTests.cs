using ControlR.Libraries.Api.Contracts.Dtos.AgentApi;
using ControlR.Libraries.Shared.Services.Encryption;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlR.ApiClient.Tests;

public class AgentSignatureHeaderTests
{
  [Fact]
  public void EncodeThenDecode_RoundTripsTheSignature()
  {
    var keyProvider = CreateKeyProvider();
    var keyPair = keyProvider.GenerateKeyPair();
    var deviceId = Guid.NewGuid();
    var attestation = new AgentRequestAttestationDto(deviceId, "GET", "/api/agent/deployment-options");

    var signedDto = keyProvider.Sign(attestation, keyPair.PrivateKey);
    var encoded = AgentSignatureHeader.Encode(signedDto);

    Assert.True(AgentSignatureHeader.TryDecode(encoded, out var decoded));
    Assert.Equal(deviceId, decoded!.Dto.DeviceId);
    Assert.Equal("GET", decoded.Dto.Method);
    Assert.Equal("/api/agent/deployment-options", decoded.Dto.PathAndQuery);

    // The server verifies against the key it stored, so the envelope has to survive the header.
    Assert.True(keyProvider.Verify(decoded, keyPair.PublicKey));
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("not base64 at all!!")]
  [InlineData("c29tZSBub24tbXNncGFjayBkYXRh")]
  public void TryDecode_WithAValueThatIsNotASignedPayload_ReturnsFalse(string? headerValue)
  {
    Assert.False(AgentSignatureHeader.TryDecode(headerValue, out _));
  }

  private static Ed25519KeyProvider CreateKeyProvider()
  {
    return new Ed25519KeyProvider(TimeProvider.System, NullLogger<Ed25519KeyProvider>.Instance);
  }
}
