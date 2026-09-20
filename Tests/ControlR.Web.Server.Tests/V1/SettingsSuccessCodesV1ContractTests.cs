using System.Text.Json;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// The success responses the settings POSTs publish in the committed V1 document.
/// </summary>
public class SettingsSuccessCodesV1ContractTests
{
  [Theory]
  [InlineData(HttpConstants.V1.TenantSettingsEndpoint, "#/components/schemas/TenantSettingResponseDto")]
  [InlineData(HttpConstants.V1.UserPreferencesEndpoint, "#/components/schemas/UserPreferenceResponseDto")]
  public void SettingsPost_DeclaresBothOkAndCreated(string path, string schemaReference)
  {
    var documentPath = Path.Combine(
      FindRepositoryRoot(),
      "ControlR.Web.Server",
      "ControlR.Web.Server_v1.json");

    Assert.True(File.Exists(documentPath), $"Missing committed OpenAPI document: {documentPath}");

    using var document = JsonDocument.Parse(File.ReadAllText(documentPath));

    var responses = document.RootElement
      .GetProperty("paths")
      .GetProperty(path)
      .GetProperty("post")
      .GetProperty("responses");

    string[] successStatusCodes = ["200", "201"];

    foreach (var statusCode in successStatusCodes)
    {
      Assert.True(
        responses.TryGetProperty(statusCode, out var response),
        $"POST {path} does not declare {statusCode}, but answers it when a blank value removes the " +
        "setting. A client generated from this document has no case for the response it receives.");

      var declaresDto =
        response.TryGetProperty("content", out var content) &&
        content.TryGetProperty("application/json", out var mediaType) &&
        mediaType.TryGetProperty("schema", out var schema) &&
        schema.TryGetProperty("$ref", out var reference) &&
        reference.GetString() == schemaReference;

      Assert.True(
        declaresDto,
        $"POST {path} declares {statusCode} without the {schemaReference} schema.");
    }
  }

  private static string FindRepositoryRoot()
  {
    var current = new DirectoryInfo(AppContext.BaseDirectory);

    while (current is not null)
    {
      if (File.Exists(Path.Combine(current.FullName, "ControlR.slnx")))
      {
        return current.FullName;
      }

      current = current.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate repository root containing ControlR.slnx.");
  }
}
