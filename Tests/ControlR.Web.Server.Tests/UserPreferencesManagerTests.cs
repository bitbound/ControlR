using System.Globalization;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Api.Contracts.Settings;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
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

  [Fact]
  public async Task GetAllPreferences_WhenNoStoredValuesExist_ReturnsDefaults()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    var result = await _userPreferencesManager.GetAllPreferences(user.Id, cancellationToken);

    Assert.Equal(UserPreferenceDefinitions.DefaultAutoQualityLowerThresholdMbps, result.AutoQualityLowerThresholdMbps);
    Assert.Equal(UserPreferenceDefinitions.DefaultAutoQualityMaximum, result.AutoQualityMaximum);
    Assert.Equal(UserPreferenceDefinitions.DefaultAutoQualityMinimum, result.AutoQualityMinimum);
    Assert.Equal(UserPreferenceDefinitions.DefaultAutoQualityUpperThresholdMbps, result.AutoQualityUpperThresholdMbps);
    Assert.Equal(UserPreferenceDefinitions.DefaultCaptureCursor, result.CaptureCursor);
    Assert.Equal(UserPreferenceDefinitions.DefaultHideOfflineDevices, result.HideOfflineDevices);
    Assert.Equal(UserPreferenceDefinitions.DefaultIncludeUntaggedDevices, result.IncludeUntaggedDevices);
    Assert.Equal(UserPreferenceDefinitions.DefaultNotifyUserOnSessionStart, result.NotifyUserOnSessionStart);
    Assert.Equal(ThemeMode.Auto, result.ThemeMode);
    Assert.Equal(ViewMode.Fit, result.ViewMode);
    Assert.Equal(string.Empty, result.UserDisplayName);
  }

  [Fact]
  public async Task GetAllPreferences_WhenStoredValuesExist_ReturnsStoredValuesAndFallsBackForInvalidEntries()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await using (var arrangeScope = _testApp.Services.CreateAsyncScope())
    {
      var db = arrangeScope.ServiceProvider.GetRequiredService<AppDb>();
      db.UserPreferences.AddRange(
      [
        new UserPreference
        {
          Name = UserPreferenceNames.UserDisplayName,
          UserId = user.Id,
          Value = "Display Name"
        },
        new UserPreference
        {
          Name = UserPreferenceNames.NotifyUserOnSessionStart,
          UserId = user.Id,
          Value = bool.FalseString
        },
        new UserPreference
        {
          Name = UserPreferenceNames.ThemeMode,
          UserId = user.Id,
          Value = ThemeMode.Dark.ToString()
        },
        new UserPreference
        {
          Name = UserPreferenceNames.ManualQuality,
          UserId = user.Id,
          Value = "invalid-number"
        }
      ]);

      await db.SaveChangesAsync(cancellationToken);
    }

    var result = await _userPreferencesManager.GetAllPreferences(user.Id, cancellationToken);

    Assert.Equal("Display Name", result.UserDisplayName);
    Assert.False(result.NotifyUserOnSessionStart);
    Assert.Equal(ThemeMode.Dark, result.ThemeMode);
    Assert.Equal(UserPreferenceDefinitions.DefaultManualQuality, result.ManualQuality);
  }

  public async ValueTask InitializeAsync()
  {
    _testApp = await TestAppBuilder.CreateTestApp(_testOutput, testDatabaseName: $"{Guid.NewGuid()}");
    _userPreferencesManager = _testApp.Services.GetRequiredService<IUserPreferencesManager>();
  }

  [Fact]
  public async Task SetPreferences_WhenAnyValueIsInvalid_DoesNotPersistChanges()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await _userPreferencesManager.SetPreference(
      user.Id,
      new UserPreferenceRequestDto(UserPreferenceNames.ThemeMode, ThemeMode.Dark.ToString()),
      cancellationToken);

    var result = await _userPreferencesManager.SetPreferences(
      user.Id,
      new UserPreferencesDto(
        6.5,
        85,
        25,
        16.5,
        true,
        false,
        true,
        true,
        true,
        KeyboardInputMode.Physical,
        70,
        20.5,
        false,
        false,
        (ThemeMode)999,
        "Updated Name",
        ViewMode.Stretch),
      cancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.ValidationFailed, result.ErrorCode);

    var preferences = await _userPreferencesManager.GetAllPreferences(user.Id, cancellationToken);
    Assert.Equal(ThemeMode.Dark, preferences.ThemeMode);
    Assert.Equal(string.Empty, preferences.UserDisplayName);
  }

  [Fact]
  public async Task SetPreferences_WhenCurrentCultureUsesCommaDecimal_PersistsInvariantNumericStrings()
  {
    var originalCulture = CultureInfo.CurrentCulture;
    CultureInfo.CurrentCulture = new CultureInfo("de-DE");
    try
    {
      var cancellationToken = TestContext.Current.CancellationToken;
      var tenant = await _testApp.Services.CreateTestTenant();
      var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

      var result = await _userPreferencesManager.SetPreferences(
        user.Id,
        new UserPreferencesDto(
          6.5,
          85,
          25,
          16.5,
          true,
          false,
            true,
          true,
          true,
          KeyboardInputMode.Physical,
          70,
          20.5,
          false,
          false,
          ThemeMode.Light,
          string.Empty,
          ViewMode.Stretch),
        cancellationToken);

      Assert.True(result.IsSuccess);

      await using var assertScope = _testApp.Services.CreateAsyncScope();
      var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDb>();
      var lowerThresholdPreference = await assertDb.UserPreferences
        .FirstAsync(x => x.UserId == user.Id && x.Name == UserPreferenceNames.AutoQualityLowerThresholdMbps, cancellationToken);
      var upperThresholdPreference = await assertDb.UserPreferences
        .FirstAsync(x => x.UserId == user.Id && x.Name == UserPreferenceNames.AutoQualityUpperThresholdMbps, cancellationToken);
      var bandwidthPreference = await assertDb.UserPreferences
        .FirstAsync(x => x.UserId == user.Id && x.Name == UserPreferenceNames.MaxBandwidthMbps, cancellationToken);

      Assert.Equal("6.5", lowerThresholdPreference.Value);
      Assert.Equal("16.5", upperThresholdPreference.Value);
      Assert.Equal("20.5", bandwidthPreference.Value);
    }
    finally
    {
      CultureInfo.CurrentCulture = originalCulture;
    }
  }

  [Fact]
  public async Task SetPreferences_WhenDtoIsValid_UpdatesAllPreferences()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"user-{Guid.NewGuid():N}@test.local");

    await _userPreferencesManager.SetPreference(
      user.Id,
      new UserPreferenceRequestDto(UserPreferenceNames.UserDisplayName, "Existing Name"),
      cancellationToken);

    var result = await _userPreferencesManager.SetPreferences(
      user.Id,
      new UserPreferencesDto(
        6.5,
        85,
        25,
        16.5,
        true,
        false,
        true,
        true,
        true,
        KeyboardInputMode.Physical,
        70,
        20.5,
        false,
        false,
        ThemeMode.Light,
        string.Empty,
        ViewMode.Stretch),
      cancellationToken);

    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
    Assert.Equal(6.5, result.Value.AutoQualityLowerThresholdMbps);
    Assert.Equal(85, result.Value.AutoQualityMaximum);
    Assert.Equal(25, result.Value.AutoQualityMinimum);
    Assert.Equal(16.5, result.Value.AutoQualityUpperThresholdMbps);
    Assert.True(result.Value.CaptureCursor);
    Assert.False(result.Value.HideOfflineDevices);
    Assert.True(result.Value.IncludeUntaggedDevices);
    Assert.True(result.Value.IsAutoQualityEnabled);
    Assert.True(result.Value.IsMaxBandwidthEnabled);
    Assert.Equal(KeyboardInputMode.Physical, result.Value.KeyboardInputMode);
    Assert.Equal(70, result.Value.ManualQuality);
    Assert.Equal(20.5, result.Value.MaxBandwidthMbps);
    Assert.False(result.Value.NotifyUserOnSessionStart);
    Assert.False(result.Value.OpenDeviceInNewTab);
    Assert.Equal(ThemeMode.Light, result.Value.ThemeMode);
    Assert.Equal(string.Empty, result.Value.UserDisplayName);
    Assert.Equal(ViewMode.Stretch, result.Value.ViewMode);

    await using var assertScope = _testApp.Services.CreateAsyncScope();
    var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDb>();
    var displayNamePreference = await assertDb.UserPreferences
      .FirstOrDefaultAsync(x => x.UserId == user.Id && x.Name == UserPreferenceNames.UserDisplayName, cancellationToken);

    Assert.Null(displayNamePreference);
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