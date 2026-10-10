using ControlR.Libraries.Shared.Extensions;
using ControlR.Web.Client.Helpers;

namespace ControlR.Web.Client.Tests;

/// <summary>
/// Pins the device-half origin rule: the server's public base URL when the server publishes one,
/// otherwise the browser's own address.
/// </summary>
public class DeviceRelayUriTests
{
  [Fact]
  public void ResolveBaseUri_PublicBaseUrlSet_ReturnsPublicOrigin()
  {
    var baseUri = DeviceRelayUri.ResolveBaseUri("https://relay.example.com", "http://192.168.1.50:5000/");

    Assert.Equal("https", baseUri.Scheme);
    Assert.Equal("relay.example.com", baseUri.Host);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void ResolveBaseUri_PublicBaseUrlUnset_ReturnsBrowserOrigin(string? publicBaseUrl)
  {
    var baseUri = DeviceRelayUri.ResolveBaseUri(publicBaseUrl, "http://192.168.1.50:5000/");

    Assert.Equal("http", baseUri.Scheme);
    Assert.Equal("192.168.1.50", baseUri.Host);
    Assert.Equal(5000, baseUri.Port);
  }

  [Theory]
  [InlineData("https://relay.example.com", "wss", "relay.example.com")]
  [InlineData("http://relay.example.com:9080", "ws", "relay.example.com:9080")]
  public void WebsocketConversion_PublicBaseUrlSet_KeepsPublicAuthority(
    string publicBaseUrl,
    string expectedScheme,
    string expectedAuthority)
  {
    var relayBaseUri = DeviceRelayUri
      .ResolveBaseUri(publicBaseUrl, "http://192.168.1.50:5000/")
      .ToWebsocketUri();

    Assert.Equal(expectedScheme, relayBaseUri.Scheme);
    Assert.Equal(expectedAuthority, relayBaseUri.Authority);
  }

  [Fact]
  public void WebsocketConversion_PublicBaseUrlUnset_KeepsBrowserAuthority()
  {
    var relayBaseUri = DeviceRelayUri
      .ResolveBaseUri(null, "https://192.168.1.50:5000/")
      .ToWebsocketUri();

    Assert.Equal("wss", relayBaseUri.Scheme);
    Assert.Equal("192.168.1.50:5000", relayBaseUri.Authority);
  }
}
