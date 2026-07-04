using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using ControlR.Web.Server.Services.Settings;

namespace ControlR.Web.Server.Tests;

public class UserStorageManagerTests(ITestOutputHelper testOutput) : IAsyncLifetime
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  private TestApp _testApp = null!;
  private IUserStorageManager _userStorageManager = null!;

  [Fact]
  public async Task DeleteAsync_WhenKeyDoesNotExist_ReturnsFalse()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    var deleted = await _userStorageManager.Delete("nonexistent-key", user.Id, cancellationToken);

    Assert.False(deleted);
  }

  [Fact]
  public async Task DeleteAsync_WhenKeyExists_RemovesEntry()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await _userStorageManager.Set(UserStorageKeys.AcknowledgedNewVersion, "1.0.0.0", user.Id, cancellationToken);

    var deleted = await _userStorageManager.Delete(UserStorageKeys.AcknowledgedNewVersion, user.Id, cancellationToken);

    Assert.True(deleted);

    var value = await _userStorageManager.Get(UserStorageKeys.AcknowledgedNewVersion, user.Id, cancellationToken);
    Assert.Null(value);
  }

  [Fact]
  public async Task DeleteAsync_WhenKeyIsEmpty_ThrowsArgumentException()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await Assert.ThrowsAsync<ArgumentException>(() =>
        _userStorageManager.Delete("", user.Id, cancellationToken));
  }

  [Fact]
  public async Task DeleteAsync_WhenKeyIsNull_ThrowsArgumentNullException()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await Assert.ThrowsAsync<ArgumentNullException>(() =>
        _userStorageManager.Delete(null!, user.Id, cancellationToken));
  }

  public async ValueTask DisposeAsync()
  {
    await _testApp.DisposeAsync();
  }

  [Fact]
  public async Task GetAsync_WhenKeyDoesNotExist_ReturnsNull()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    var result = await _userStorageManager.Get("some-key", user.Id, cancellationToken);

    Assert.Null(result);
  }

  [Fact]
  public async Task GetAsync_WhenKeyIsEmpty_ThrowsArgumentException()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await Assert.ThrowsAsync<ArgumentException>(() =>
        _userStorageManager.Get("", user.Id, cancellationToken));
  }

  [Fact]
  public async Task GetAsync_WhenKeyIsNull_ThrowsArgumentNullException()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await Assert.ThrowsAsync<ArgumentNullException>(() =>
        _userStorageManager.Get(null!, user.Id, cancellationToken));
  }

  public async ValueTask InitializeAsync()
  {
    _testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    _userStorageManager = _testApp.Services.GetRequiredService<IUserStorageManager>();
  }

  [Fact]
  public async Task SetAsync_DifferentUsers_HaveSeparateStorage()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user1 = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user1-{Guid.NewGuid():N}@test.local");
    var user2 = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user2-{Guid.NewGuid():N}@test.local");

    await _userStorageManager.Set(UserStorageKeys.AcknowledgedNewVersion, "1.0.0.0", user1.Id, cancellationToken);
    await _userStorageManager.Set(UserStorageKeys.AcknowledgedNewVersion, "2.0.0.0", user2.Id, cancellationToken);

    var value1 = await _userStorageManager.Get(UserStorageKeys.AcknowledgedNewVersion, user1.Id, cancellationToken);
    var value2 = await _userStorageManager.Get(UserStorageKeys.AcknowledgedNewVersion, user2.Id, cancellationToken);

    Assert.Equal("1.0.0.0", value1!.Value);
    Assert.Equal("2.0.0.0", value2!.Value);
  }

  [Fact]
  public async Task SetAsync_WhenKeyDoesNotExists_CreatesNewEntry()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    var result = await _userStorageManager.Set(UserStorageKeys.AcknowledgedNewVersion, "1.0.0.0", user.Id, cancellationToken);

    Assert.NotNull(result);
    Assert.Equal(UserStorageKeys.AcknowledgedNewVersion, result.Key);
    Assert.Equal("1.0.0.0", result.Value);

    var value = await _userStorageManager.Get(UserStorageKeys.AcknowledgedNewVersion, user.Id, cancellationToken);
    Assert.Equal("1.0.0.0", value!.Value);
  }

  [Fact]
  public async Task SetAsync_WhenKeyExists_UpdatesValue()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await _userStorageManager.Set(UserStorageKeys.AcknowledgedNewVersion, "1.0.0.0", user.Id, cancellationToken);

    var result = await _userStorageManager.Set(UserStorageKeys.AcknowledgedNewVersion, "2.0.0.0", user.Id, cancellationToken);

    Assert.NotNull(result);
    Assert.Equal(UserStorageKeys.AcknowledgedNewVersion, result.Key);
    Assert.Equal("2.0.0.0", result.Value);
  }

  [Fact]
  public async Task SetAsync_WhenKeyHasInvalidChars_ThrowsArgumentException()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await Assert.ThrowsAsync<ArgumentException>(() =>
        _userStorageManager.Set("invalid/key!", "value", user.Id, cancellationToken));
  }

  [Fact]
  public async Task SetAsync_WhenKeyIsEmpty_ThrowsArgumentException()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await Assert.ThrowsAsync<ArgumentException>(() =>
        _userStorageManager.Set("", "value", user.Id, cancellationToken));
  }

  [Fact]
  public async Task SetAsync_WhenKeyIsNull_ThrowsArgumentNullException()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await Assert.ThrowsAsync<ArgumentNullException>(() =>
        _userStorageManager.Set(null!, "value", user.Id, cancellationToken));
  }

  [Fact]
  public async Task SetAsync_WhenValueExceedsMaxLength_ThrowsArgumentException()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    var longValue = new string('x', UserStorageItem.MaxValueLength + 1);

    await Assert.ThrowsAsync<ArgumentException>(() =>
        _userStorageManager.Set("valid-key", longValue, user.Id, cancellationToken));
  }

  [Fact]
  public async Task SetAsync_WhenValueIsEmpty_StoresEmptyString()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await _userStorageManager.Set("empty-value-key", "", user.Id, cancellationToken);

    var result = await _userStorageManager.Get("empty-value-key", user.Id, cancellationToken);

    Assert.NotNull(result);
    Assert.Equal("", result.Value);
  }
}
