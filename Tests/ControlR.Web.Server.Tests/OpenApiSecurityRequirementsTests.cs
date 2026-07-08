using System.Net.Http.Json;
using System.Text.Json;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlR.Web.Server.Tests;

public class OpenApiSecurityRequirementsTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task OpenApi_Document_MeetsSecurityRequirements()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: new Dictionary<string, string?>
      {
        { "AppOptions:EnableScalarUi", "true" },
      });

    var response = await testServer.Factory.CreateClient()
      .GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
    var doc = JsonDocument.Parse(response).RootElement;

    Assert.True(doc.TryGetProperty("paths", out var paths));

    AssertAuthEndpoints(paths);
    AssertLogonTokenEndpoints(paths);

    Assert.True(doc.TryGetProperty("components", out var components));
    Assert.True(components.TryGetProperty("securitySchemes", out var schemes));
    Assert.True(schemes.TryGetProperty("Cookie", out _));
    Assert.True(schemes.TryGetProperty("PersonalAccessToken", out _));
    Assert.True(schemes.TryGetProperty("ServiceAccountCredential", out _));
  }

  private void AssertAuthEndpoints(JsonElement paths)
  {
    Assert.True(paths.TryGetProperty("/api/auth/register", out var registerEndpoint));
    Assert.True(registerEndpoint.TryGetProperty("post", out var registerPost));
    if (registerPost.TryGetProperty("security", out var registerSecurity))
    {
      foreach (var req in registerSecurity.EnumerateArray())
      {
        Assert.True(
          req.EnumerateObject().All(p => p.Value.GetArrayLength() == 0),
          "AllowAnonymous endpoint should not have security requirements");
      }
    }
  }

  private void AssertLogonTokenEndpoints(JsonElement paths)
  {
    var logonPost = paths.GetProperty("/internal/logon-tokens").GetProperty("post");

    _testOutput.WriteLine($"POST /internal/logon-tokens JSON: {logonPost.GetRawText()}");

    Assert.True(logonPost.TryGetProperty("security", out var logonSecurity), "security property should be present on the endpoint");

    var schemesUsed = new HashSet<string>();
    foreach (var req in logonSecurity.EnumerateArray())
    {
      foreach (var prop in req.EnumerateObject())
      {
        schemesUsed.Add(prop.Name);
      }
    }

    Assert.Contains("Cookie", schemesUsed);
    Assert.Contains("PersonalAccessToken", schemesUsed);
  }
}
