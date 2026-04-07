using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Client.Models;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ControlR.Web.Server.Services.Settings;

namespace ControlR.Web.Server.Tests;

public class UserPreferencesManagerTests(ITestOutputHelper testOutput) : IAsyncLifetime
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  private TestApp _testApp = null!;
  private IUserPreferencesManager _userPreferencesManager = null!;

  public async ValueTask DisposeAsync()
  {
    await _testApp.DisposeAsync();
  }

  public async ValueTask InitializeAsync()
  {
    _testApp = await TestAppBuilder.CreateTestApp(_testOutput, testDatabaseName: $"{Guid.NewGuid()}");
    _userPreferencesManager = _testApp.Services.GetRequiredService<IUserPreferencesManager>();
  }

  [Fact]
  public async Task SetPreference_WhenBooleanValueIsInvalid_ReturnsValidationFailed()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    var result = await _userPreferencesManager.SetPreference(
      user.Id,
      new UserPreferenceRequestDto(UserPreferenceNames.HideOfflineDevices, "not-a-bool"),
      cancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.ValidationFailed, result.ErrorCode);
  }

  [Fact]
  public async Task SetPreference_WhenDisplayNameContainsInvalidCharacters_ReturnsValidationFailed()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    var result = await _userPreferencesManager.SetPreference(
      user.Id,
      new UserPreferenceRequestDto(UserPreferenceNames.UserDisplayName, "bad$name"),
      cancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.ValidationFailed, result.ErrorCode);
  }

  [Fact]
  public async Task SetPreference_WhenDisplayNameIsTooLong_ReturnsValidationFailed()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    var result = await _userPreferencesManager.SetPreference(
      user.Id,
      new UserPreferenceRequestDto(UserPreferenceNames.UserDisplayName, new string('a', 26)),
      cancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.ValidationFailed, result.ErrorCode);
  }

  [Fact]
  public async Task SetPreference_WhenEnumValueIsInvalid_ReturnsValidationFailed()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    var result = await _userPreferencesManager.SetPreference(
      user.Id,
      new UserPreferenceRequestDto(UserPreferenceNames.ThemeMode, "neon"),
      cancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.ValidationFailed, result.ErrorCode);
  }

  [Fact]
  public async Task SetPreference_WhenPreferenceExists_UpdatesExistingValue()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await _userPreferencesManager.SetPreference(
      user.Id,
      new UserPreferenceRequestDto(UserPreferenceNames.ThemeMode, ThemeMode.Auto.ToString()),
      cancellationToken);

    var result = await _userPreferencesManager.SetPreference(
      user.Id,
      new UserPreferenceRequestDto(UserPreferenceNames.ThemeMode, ThemeMode.Dark.ToString()),
      cancellationToken);

    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
    Assert.Equal(ThemeMode.Dark.ToString(), result.Value.Value);

    await using var assertScope = _testApp.Services.CreateAsyncScope();
    var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDb>();
    var storedPreferences = await assertDb.UserPreferences
      .Where(x => x.UserId == user.Id && x.Name == UserPreferenceNames.ThemeMode)
      .ToListAsync(cancellationToken);

    Assert.Single(storedPreferences);
    Assert.Equal(ThemeMode.Dark.ToString(), storedPreferences[0].Value);
  }

  [Fact]
  public async Task SetPreference_WhenPreferenceIsValid_CreatesPreference()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    var result = await _userPreferencesManager.SetPreference(
      user.Id,
      new UserPreferenceRequestDto(UserPreferenceNames.UserDisplayName, " Test User "),
      cancellationToken);

    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
    Assert.Equal("Test User", result.Value.Value);

    await using var assertScope = _testApp.Services.CreateAsyncScope();
    var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDb>();
    var storedPreference = await assertDb.UserPreferences
      .FirstOrDefaultAsync(x => x.UserId == user.Id && x.Name == UserPreferenceNames.UserDisplayName, cancellationToken);

    Assert.NotNull(storedPreference);
    Assert.Equal("Test User", storedPreference.Value);
  }

  [Fact]
  public async Task SetPreference_WhenUserDoesNotExist_ReturnsNotFound()
  {
    var cancellationToken = TestContext.Current.CancellationToken;

    var result = await _userPreferencesManager.SetPreference(
      Guid.NewGuid(),
      new UserPreferenceRequestDto(UserPreferenceNames.UserDisplayName, "Test User"),
      cancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.NotFound, result.ErrorCode);
  }
}