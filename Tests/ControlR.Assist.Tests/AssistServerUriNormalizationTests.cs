using ControlR.Assist.Services;

namespace ControlR.Assist.Tests;

public class AssistServerUriNormalizationTests
{
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("sdf")]
  [InlineData("localhost:99999")]
  [InlineData("http://")]
  [InlineData("https://")]
  [InlineData("https:///")]
  [InlineData("http://.")]
  [InlineData("ftp://server.example.com")]
  [InlineData("https://server.example.com path")]
  [InlineData("https://ser ver.example.com")]
  [InlineData("not a url at all")]
  [InlineData("://server.example.com")]
  public void Invalid(string? input)
  {
    Assert.False(AssistSessionCoordinator.TryNormalizeServerUri(input, out _));
  }

  [Theory]
  [InlineData("https://server.example.com", "https://server.example.com/")]
  [InlineData("http://server.example.com", "http://server.example.com/")]
  [InlineData("server.example.com", "https://server.example.com/")]
  [InlineData("  server.example.com  ", "https://server.example.com/")]
  [InlineData("SERVER.Example.COM", "https://server.example.com/")]
  [InlineData("https://server.example.com:8443", "https://server.example.com:8443/")]
  [InlineData("server.example.com:8443", "https://server.example.com:8443/")]
  [InlineData("localhost", "https://localhost/")]
  [InlineData("https://localhost:5001", "https://localhost:5001/")]
  [InlineData("192.168.1.5", "https://192.168.1.5/")]
  [InlineData("https://192.168.1.5:8443", "https://192.168.1.5:8443/")]
  [InlineData("https://server.example.com/assist?code=123#frag", "https://server.example.com/")]
  [InlineData("https://server.example.com/some/path", "https://server.example.com/")]
  public void Valid(string input, string expected)
  {
    Assert.True(AssistSessionCoordinator.TryNormalizeServerUri(input, out var uri));
    Assert.NotNull(uri);
    Assert.Equal(expected, uri.ToString());
  }
}
