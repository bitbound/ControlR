using System.Text.Json;
using ControlR.Web.Server.Tests.Helpers;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Ratchet for the raw-body endpoints. An action that writes bytes into the response itself has to say
/// so with <c>[BinaryResponse]</c>, and an action that reads a multipart form has to say so with
/// <c>[MultipartRequestBody]</c>. Without the first, the published contract carries a success response
/// that names no media type, which is what a streamed endpoint looks like when it goes undocumented.
/// </summary>
public class V1BinaryBodyContractTests(ITestOutputHelper testOutput)
{
  /// <summary>
  /// Answers with a bare 200 and no body, so there is no media type to name.
  /// </summary>
  private const string BodilessSuccess = "POST /api/v1/test-email";

  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task Document_EverySuccessResponse_DeclaresAMediaType()
  {
    using var document = await LoadDocument("/openapi/v1.json");

    var offenders = new List<string>();
    foreach (var (method, path, status) in EnumerateResponsesWithoutContent(document.RootElement, "2"))
    {
      if (status == "204" || $"{method} {path}" == BodilessSuccess)
      {
        continue;
      }

      offenders.Add($"{method} {path} {status}");
    }

    foreach (var offender in offenders)
    {
      _testOutput.WriteLine($"No media type declared for {offender}.");
    }

    Assert.Empty(offenders);
  }

  [Fact]
  public async Task Document_UploadOperation_DeclaresMultipartFormParts()
  {
    using var document = await LoadDocument("/openapi/v1.json");
    var upload = document.RootElement
      .GetProperty("paths")
      .GetProperty($"{HttpConstants.V1.DeviceFileSystemEndpoint}/upload/{{deviceId}}")
      .GetProperty("post");

    var form = upload.GetProperty("requestBody").GetProperty("content").GetProperty("multipart/form-data");
    var schema = form.GetProperty("schema");
    var properties = schema.GetProperty("properties");

    Assert.True(properties.TryGetProperty("file", out var file));
    Assert.Equal("binary", file.GetProperty("format").GetString());
    Assert.True(properties.TryGetProperty("targetSaveDirectory", out _));
    Assert.True(properties.TryGetProperty("overwrite", out _));

    var required = schema.GetProperty("required").EnumerateArray()
      .Select(element => element.GetString() ?? string.Empty)
      .ToHashSet(StringComparer.Ordinal);

    Assert.Equal(new HashSet<string> { "file", "targetSaveDirectory" }, required);
  }

  /// <summary>
  /// Yields every operation response whose status starts with <paramref name="statusPrefix"/> and
  /// declares no content.
  /// </summary>
  private static IEnumerable<(string Method, string Path, string Status)> EnumerateResponsesWithoutContent(
    JsonElement document,
    string statusPrefix)
  {
    foreach (var pathItem in document.GetProperty("paths").EnumerateObject())
    {
      foreach (var operation in pathItem.Value.EnumerateObject())
      {
        if (operation.Value.ValueKind != JsonValueKind.Object ||
            !operation.Value.TryGetProperty("responses", out var responses))
        {
          continue;
        }

        foreach (var response in responses.EnumerateObject())
        {
          if (!response.Name.StartsWith(statusPrefix, StringComparison.Ordinal) ||
              response.Value.TryGetProperty("content", out _))
          {
            continue;
          }

          yield return (operation.Name.ToUpperInvariant(), pathItem.Name, response.Name);
        }
      }
    }
  }

  private async Task<JsonDocument> LoadDocument(string path)
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: new Dictionary<string, string?>
      {
        { "AppOptions:EnableScalarUi", "true" },
      });

    var json = await testServer.Factory.CreateClient()
      .GetStringAsync(path, TestContext.Current.CancellationToken);

    return JsonDocument.Parse(json);
  }
}
