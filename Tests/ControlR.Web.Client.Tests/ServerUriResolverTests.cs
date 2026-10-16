using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Web.Client.Services;
using Microsoft.AspNetCore.Components;

namespace ControlR.Web.Client.Tests;

/// <summary>
/// Pins the server origin rule: the server's public base URL when the server publishes one,
/// otherwise the browser's own address.
/// </summary>
public class ServerUriResolverTests
{
  private const string BrowserHttpsOrigin = "https://192.168.1.50:5000/";
  private const string BrowserOrigin = "http://192.168.1.50:5000/";

  [Fact]
  public async Task ResolveBaseUriAsync_PublicBaseUrlSet_ReturnsPublicOrigin()
  {
    var baseUri = await CreateResolver("https://relay.example.com").ResolveBaseUriAsync();

    Assert.Equal("https", baseUri.Scheme);
    Assert.Equal("relay.example.com", baseUri.Host);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public async Task ResolveBaseUriAsync_PublicBaseUrlUnset_ReturnsBrowserOrigin(string? publicBaseUrl)
  {
    var baseUri = await CreateResolver(publicBaseUrl).ResolveBaseUriAsync();

    Assert.Equal("http", baseUri.Scheme);
    Assert.Equal("192.168.1.50", baseUri.Host);
    Assert.Equal(5000, baseUri.Port);
  }

  [Theory]
  [InlineData("https://relay.example.com", "wss", "relay.example.com")]
  [InlineData("http://relay.example.com:9080", "ws", "relay.example.com:9080")]
  public async Task WebsocketConversion_PublicBaseUrlSet_KeepsPublicAuthority(
    string publicBaseUrl,
    string expectedScheme,
    string expectedAuthority)
  {
    var relayBaseUri = (await CreateResolver(publicBaseUrl).ResolveBaseUriAsync()).ToWebsocketUri();

    Assert.Equal(expectedScheme, relayBaseUri.Scheme);
    Assert.Equal(expectedAuthority, relayBaseUri.Authority);
  }

  [Fact]
  public async Task WebsocketConversion_PublicBaseUrlUnset_KeepsBrowserAuthority()
  {
    var relayBaseUri = (await CreateResolver(null, BrowserHttpsOrigin).ResolveBaseUriAsync()).ToWebsocketUri();

    Assert.Equal("wss", relayBaseUri.Scheme);
    Assert.Equal("192.168.1.50:5000", relayBaseUri.Authority);
  }

  private static ServerUriResolver CreateResolver(
    string? publicBaseUrl,
    string browserOrigin = BrowserOrigin)
  {
    return new ServerUriResolver(
      new StubPublicServerSettingsProvider(publicBaseUrl),
      new FakeNavigationManager(browserOrigin));
  }

  private sealed class FakeNavigationManager : NavigationManager
  {
    public FakeNavigationManager(string baseUri)
    {
      Initialize(baseUri, baseUri);
    }

    protected override void NavigateToCore(string uri, NavigationOptions options)
    {
      Uri = ToAbsoluteUri(uri).ToString();
    }
  }

  private sealed class StubPublicServerSettingsProvider(string? publicBaseUrl) : IPublicServerSettingsProvider
  {
    public Task<PublicServerSettings> GetPublicServerSettings()
    {
      return Task.FromResult(new PublicServerSettings(
        IsPublicRegistrationEnabled: false,
        DisableDesktopPreview: false,
        PublicBaseUrl: publicBaseUrl));
    }
  }
}
