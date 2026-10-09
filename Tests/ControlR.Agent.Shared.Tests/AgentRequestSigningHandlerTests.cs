using ControlR.Agent.Shared.Services;
using ControlR.Libraries.Api.Contracts.Dtos.AgentApi;
using ControlR.Libraries.Shared.Services.Encryption;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.Agent.Shared.Tests;

public class AgentRequestSigningHandlerTests
{
  [Fact]
  public async Task SendAsync_SignsTheRequestSoTheServerCanVerifyIt()
  {
    var deviceId = Guid.NewGuid();
    var keyProvider = new Ed25519KeyProvider(TimeProvider.System, NullLogger<Ed25519KeyProvider>.Instance);
    var keyPair = keyProvider.GenerateKeyPair();
    var optionsAccessor = new Mock<IOptionsAccessor>();
    optionsAccessor.SetupGet(x => x.DeviceId).Returns(deviceId);
    optionsAccessor.SetupGet(x => x.PrivateKey).Returns(Convert.ToBase64String(keyPair.PrivateKey));

    var handler = new AgentRequestSigningHandler(optionsAccessor.Object, keyProvider)
    {
      InnerHandler = new CapturingHandler()
    };
    var invoker = new HttpMessageInvoker(handler);
    using var request = new HttpRequestMessage(HttpMethod.Get, "https://controlr.example/api/agent/deployment-options?case=x");

    await invoker.SendAsync(request, CancellationToken.None);

    Assert.True(request.Headers.TryGetValues(AgentSignatureHeader.Name, out var values));
    Assert.True(AgentSignatureHeader.TryDecode(values!.Single(), out var signedDto));

    // The verb and path travel inside the signature, which is what stops a header captured from one
    // call being replayed against another.
    Assert.Equal(deviceId, signedDto!.Dto.DeviceId);
    Assert.Equal("GET", signedDto.Dto.Method);
    Assert.Equal("/api/agent/deployment-options?case=x", signedDto.Dto.PathAndQuery);

    // The server verifies against the public key it already stored for the device.
    Assert.True(keyProvider.Verify(signedDto, keyPair.PublicKey));
  }

  [Fact]
  public async Task SendAsync_WhenThereIsNoSigningKey_SendsTheRequestUnsigned()
  {
    var keyProvider = new Ed25519KeyProvider(TimeProvider.System, NullLogger<Ed25519KeyProvider>.Instance);
    var optionsAccessor = new Mock<IOptionsAccessor>();
    optionsAccessor.SetupGet(x => x.PrivateKey).Returns((string?)null);

    var handler = new AgentRequestSigningHandler(optionsAccessor.Object, keyProvider)
    {
      InnerHandler = new CapturingHandler()
    };
    var invoker = new HttpMessageInvoker(handler);
    using var request = new HttpRequestMessage(HttpMethod.Get, "https://controlr.example/api/agent/deployment-options");

    await invoker.SendAsync(request, CancellationToken.None);

    // An install that has not enrolled yet has nothing to sign with, and the endpoints it needs at
    // that point are anonymous.
    Assert.False(request.Headers.Contains(AgentSignatureHeader.Name));
  }

  private sealed class CapturingHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }
  }
}
