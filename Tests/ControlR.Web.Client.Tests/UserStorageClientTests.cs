#pragma warning disable BB0001 // Member order is incorrect
using System.Net;
using ControlR.ApiClient;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Web.Client.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ControlR.Web.Client.Tests;

public class UserStorageClientTests
{
  private readonly Mock<IControlrApi> _mockApi;
  private readonly Mock<IUserStorageApi> _mockUserStorageApi;
  private readonly Mock<ILogger<UserStorageClient>> _mockLogger;
  private readonly UserStorageClient _client;

  public UserStorageClientTests()
  {
    _mockUserStorageApi = new Mock<IUserStorageApi>();
    _mockApi = new Mock<IControlrApi>();
    _mockApi
      .Setup(x => x.UserStorage)
      .Returns(_mockUserStorageApi.Object);

    _mockLogger = new Mock<ILogger<UserStorageClient>>();
    _client = new UserStorageClient(
      _mockApi.Object,
      _mockLogger.Object);
  }

  [Fact]
  public async Task GetItem_WhenKeyIsCached_ReturnsCachedValue()
  {
    var cancellationToken = TestContext.Current.CancellationToken;

    // Set up the API to be called once
    _mockUserStorageApi
      .Setup(x => x.GetUserStorageItem("existing-key", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new ApiResult<UserStorageResponseDto>(
        new UserStorageResponseDto("existing-key", "cached-value"),
        true,
        HttpStatusCode.OK));

    // First call fetches from API
    var result1 = await _client.GetItem("existing-key", cancellationToken);
    Assert.Equal("cached-value", result1);

    // Second call should use cache, not API
    var result2 = await _client.GetItem("existing-key", cancellationToken);
    Assert.Equal("cached-value", result2);

    // Verify API was called only once
    _mockUserStorageApi.Verify(
      x => x.GetUserStorageItem("existing-key", It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task GetItem_WhenApiReturnsFailure_ReturnsNull()
  {
    var cancellationToken = TestContext.Current.CancellationToken;

    _mockUserStorageApi
      .Setup(x => x.GetUserStorageItem("failing-key", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new ApiResult<UserStorageResponseDto>(
        null,
        false,
        HttpStatusCode.InternalServerError,
        "Server error"));

    var result = await _client.GetItem("failing-key", cancellationToken);

    Assert.Null(result);
  }

  [Fact]
  public async Task GetItem_WhenApiReturnsNullValue_DoesNotCacheNull()
  {
    var cancellationToken = TestContext.Current.CancellationToken;

    _mockUserStorageApi
      .Setup(x => x.GetUserStorageItem("null-value-key", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new ApiResult<UserStorageResponseDto>(
        new UserStorageResponseDto("null-value-key", null),
        true,
        HttpStatusCode.OK));

    var result1 = await _client.GetItem("null-value-key", cancellationToken);
    Assert.Null(result1);

    // API should be called again since null was not cached
    var result2 = await _client.GetItem("null-value-key", cancellationToken);
    Assert.Null(result2);

    _mockUserStorageApi.Verify(
      x => x.GetUserStorageItem("null-value-key", It.IsAny<CancellationToken>()),
      Times.Exactly(2));
  }

  [Fact]
  public async Task GetItem_WhenCancellationRequested_PropagatesException()
  {
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    _mockUserStorageApi
      .Setup(x => x.GetUserStorageItem("cancel-key", It.IsAny<CancellationToken>()))
      .ThrowsAsync(new OperationCanceledException());

    await Assert.ThrowsAsync<OperationCanceledException>(() =>
        _client.GetItem("cancel-key", cts.Token));
  }

  [Fact]
  public async Task GetItem_WhenApiThrowsNonCancellation_PropagatesException()
  {
    var cancellationToken = TestContext.Current.CancellationToken;

    _mockUserStorageApi
      .Setup(x => x.GetUserStorageItem("error-key", It.IsAny<CancellationToken>()))
      .ThrowsAsync(new HttpRequestException("Network error"));

    await Assert.ThrowsAsync<HttpRequestException>(() =>
        _client.GetItem("error-key", cancellationToken));
  }

  [Fact]
  public async Task SetItem_StoresValueInCacheAfterApiSuccess()
  {
    var cancellationToken = TestContext.Current.CancellationToken;

    _mockUserStorageApi
      .Setup(x => x.SetUserStorageItem(It.IsAny<UserStorageRequestDto>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new ApiResult<UserStorageResponseDto>(
        new UserStorageResponseDto("my-key", "my-value"),
        true,
        HttpStatusCode.OK));

    await _client.SetItem("my-key", "my-value", cancellationToken);

    // Verify the value was cached by reading it back without API call
    var cached = await _client.GetItem("my-key", cancellationToken);
    Assert.Equal("my-value", cached);
  }

  [Fact]
  public async Task SetItem_WhenApiReturnsFailure_DoesNotCache()
  {
    var cancellationToken = TestContext.Current.CancellationToken;

    _mockUserStorageApi
      .Setup(x => x.SetUserStorageItem(It.IsAny<UserStorageRequestDto>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new ApiResult<UserStorageResponseDto>(
        null,
        false,
        HttpStatusCode.InternalServerError,
        "Server error"));

    await _client.SetItem("fail-key", "fail-value", cancellationToken);

    // Should still hit API on get since not cached
    _mockUserStorageApi
      .Setup(x => x.GetUserStorageItem("fail-key", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new ApiResult<UserStorageResponseDto>(
        null,
        false,
        HttpStatusCode.NotFound,
        "Not found"));

    var result = await _client.GetItem("fail-key", cancellationToken);
    Assert.Null(result);
  }

  [Fact]
  public async Task SetItem_WhenCancellationRequested_PropagatesException()
  {
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    _mockUserStorageApi
      .Setup(x => x.SetUserStorageItem(It.IsAny<UserStorageRequestDto>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new OperationCanceledException());

    await Assert.ThrowsAsync<OperationCanceledException>(() =>
        _client.SetItem("cancel-key", "value", cts.Token));
  }

  [Fact]
  public async Task SetItem_WhenApiThrowsNonCancellation_PropagatesException()
  {
    var cancellationToken = TestContext.Current.CancellationToken;

    _mockUserStorageApi
      .Setup(x => x.SetUserStorageItem(It.IsAny<UserStorageRequestDto>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new HttpRequestException("Network error"));

    await Assert.ThrowsAsync<HttpRequestException>(() =>
        _client.SetItem("error-key", "value", cancellationToken));
  }

  [Fact]
  public async Task SetItem_WhenApiReturnsNullResponseValue_DoesNotCacheNull()
  {
    var cancellationToken = TestContext.Current.CancellationToken;

    _mockUserStorageApi
      .Setup(x => x.SetUserStorageItem(It.IsAny<UserStorageRequestDto>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new ApiResult<UserStorageResponseDto>(
        new UserStorageResponseDto("null-result", null),
        true,
        HttpStatusCode.OK));

    await _client.SetItem("null-result", "value", cancellationToken);

    // API should still be hit on get since null response was not cached
    _mockUserStorageApi
      .Setup(x => x.GetUserStorageItem("null-result", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new ApiResult<UserStorageResponseDto>(
        new UserStorageResponseDto("null-result", "actual-value"),
        true,
        HttpStatusCode.OK));

    var result = await _client.GetItem("null-result", cancellationToken);
    Assert.Equal("actual-value", result);
  }

  [Fact]
  public async Task Cache_WhenUpdatingExistingKey_DoesNotEvictOtherEntries()
  {
    var cancellationToken = TestContext.Current.CancellationToken;

    // Fill cache to near max
    for (var i = 0; i < 99; i++)
    {
      var key = $"key-{i}";
      var localKey = key;
      _mockUserStorageApi
        .Setup(x => x.GetUserStorageItem(localKey, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ApiResult<UserStorageResponseDto>(
          new UserStorageResponseDto(localKey, $"value-{i}"),
          true,
          HttpStatusCode.OK));

      await _client.GetItem(localKey, cancellationToken);
    }

    // Update an existing key - should not evict
    _mockUserStorageApi
      .Setup(x => x.SetUserStorageItem(It.IsAny<UserStorageRequestDto>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new ApiResult<UserStorageResponseDto>(
        new UserStorageResponseDto("key-0", "updated-value"),
        true,
        HttpStatusCode.OK));

    await _client.SetItem("key-0", "updated-value", cancellationToken);

    // key-0 should still be cached (updated)
    var cached = await _client.GetItem("key-0", cancellationToken);
    Assert.Equal("updated-value", cached);
  }
}
