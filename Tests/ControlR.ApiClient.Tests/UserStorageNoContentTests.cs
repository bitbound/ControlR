using System.Net;
using ControlR.ApiClient.Internal;
using ControlR.ApiClient.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlR.ApiClient.Tests;

/// <summary>
/// A key the signed-in user has never written comes back as 204 No Content with an empty body, which
/// is a successful read of no value. Reading it as JSON fails, and the caller cannot tell that apart
/// from a server that is down.
/// </summary>
public sealed class UserStorageNoContentTests
{
  private static readonly Uri _server = new("https://server.test/");

  [Fact]
  public async Task GetUserStorageItem_WhenTheKeyHasAValue_ReturnsThatValue()
  {
    var client = CreateClient(_ => RecordingHttpMessageHandler.Json("""{"key":"build-channel","value":"stable"}"""));

    var result = await client.V1.UserStorage.GetUserStorageItem(
      "build-channel",
      Guid.NewGuid(),
      TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess, result.ToString());
    Assert.Equal("stable", result.Value!.Value);
  }

  [Fact]
  public async Task GetUserStorageItem_WhenTheKeyWasNeverWritten_ReportsSuccessWithNoValue()
  {
    var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

    var result = await client.V1.UserStorage.GetUserStorageItem(
      "acknowledged-new-version",
      Guid.NewGuid(),
      TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess, result.ToString());
    Assert.NotNull(result.Value);
    Assert.Null(result.Value.Value);
  }

  [Fact]
  public async Task GetUserStorageItem_WhenTheServerRefuses_ReportsTheProblemDetailsMessage()
  {
    var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
    {
      Content = new StringContent("""{"status":403,"detail":"You may not read that key."}""", System.Text.Encoding.UTF8, "application/problem+json")
    });

    var result = await client.V1.UserStorage.GetUserStorageItem(
      "acknowledged-new-version",
      Guid.NewGuid(),
      TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Contains("You may not read that key.", result.Reason);
  }

  private static IControlrApi CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
  {
    var options = new ControlrApiClientFactoryOptions { MaxIdleClientLifetime = null };
    options.HttpMessageHandlerFactory = () => new RecordingHttpMessageHandler(responder);

    var factory = new ControlrApiClientFactory(options, TimeProvider.System, NullLoggerFactory.Instance);
    return factory.GetOrCreateClient(Guid.NewGuid().ToString("n"), o => o.BaseUrl = _server);
  }
}
