using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Settings;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services.Settings;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class TenantSettingsManagerTests(ITestOutputHelper testOutput) : IAsyncLifetime
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  private ITenantSettingsManager _tenantSettingsManager = null!;
  private TestApp _testApp = null!;

  public async ValueTask DisposeAsync()
  {
    await _testApp.DisposeAsync();
  }

  [Fact]
  public async Task GetAllSettings_WhenNoStoredValuesExist_ReturnsNullableDefaults()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();

    var result = await _tenantSettingsManager.GetAllSettings(tenant.Id, cancellationToken);

    Assert.Null(result.AppendInstanceId);
    Assert.Null(result.InstanceId);
    Assert.Null(result.NotifyUserOnSessionStart);
  }

  [Fact]
  public async Task GetAllSettings_WhenStoredValuesExist_ReturnsStoredValuesAndFallsBackForInvalidEntries()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();

    await using (var arrangeScope = _testApp.Services.CreateAsyncScope())
    {
      var db = arrangeScope.ServiceProvider.GetRequiredService<AppDb>();
      db.TenantSettings.AddRange(
      [
        new TenantSetting
        {
          Name = TenantSettingNames.AppendInstanceId,
          TenantId = tenant.Id,
          Value = bool.TrueString
        },
        new TenantSetting
        {
          Name = TenantSettingNames.InstanceId,
          TenantId = tenant.Id,
          Value = "server-alpha"
        },
        new TenantSetting
        {
          Name = TenantSettingNames.NotifyUserOnSessionStart,
          TenantId = tenant.Id,
          Value = "invalid-bool"
        }
      ]);

      await db.SaveChangesAsync(cancellationToken);
    }

    var result = await _tenantSettingsManager.GetAllSettings(tenant.Id, cancellationToken);

    Assert.True(result.AppendInstanceId);
    Assert.Equal("server-alpha", result.InstanceId);
    Assert.Equal(TenantSettingDefinitions.NotifyUserOnSessionStart.DefaultValue, result.NotifyUserOnSessionStart);
  }

  public async ValueTask InitializeAsync()
  {
    _testApp = await TestAppBuilder.CreateTestApp(_testOutput, testDatabaseName: $"{Guid.NewGuid()}");
    _tenantSettingsManager = _testApp.Services.GetRequiredService<ITenantSettingsManager>();
  }

  [Fact]
  public async Task SetSettings_WhenAnyValueIsInvalid_DoesNotPersistChanges()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();

    await _tenantSettingsManager.SetSetting(
      tenant.Id,
      new TenantSettingRequestDto(TenantSettingNames.AppendInstanceId, bool.TrueString),
      cancellationToken);

    var result = await _tenantSettingsManager.SetSettings(
      tenant.Id,
      new TenantSettingsDto(true, "default", false),
      cancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.ValidationFailed, result.ErrorCode);

    var settings = await _tenantSettingsManager.GetAllSettings(tenant.Id, cancellationToken);
    Assert.True(settings.AppendInstanceId);
    Assert.Null(settings.InstanceId);
    Assert.Null(settings.NotifyUserOnSessionStart);
  }

  [Fact]
  public async Task SetSettings_WhenDtoIsValid_ReplacesAllSettings()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();

    await _tenantSettingsManager.SetSetting(
      tenant.Id,
      new TenantSettingRequestDto(TenantSettingNames.InstanceId, "existing-instance"),
      cancellationToken);

    var result = await _tenantSettingsManager.SetSettings(
      tenant.Id,
      new TenantSettingsDto(true, null, false),
      cancellationToken);

    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
    Assert.True(result.Value.AppendInstanceId);
    Assert.Null(result.Value.InstanceId);
    Assert.False(result.Value.NotifyUserOnSessionStart);

    await using var assertScope = _testApp.Services.CreateAsyncScope();
    var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDb>();
    var instanceIdSetting = await assertDb.TenantSettings
      .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Name == TenantSettingNames.InstanceId, cancellationToken);

    Assert.Null(instanceIdSetting);
  }

  [Fact]
  public async Task SetSetting_WhenAppendInstanceIdValueIsInvalid_ReturnsValidationFailed()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();

    var result = await _tenantSettingsManager.SetSetting(
      tenant.Id,
      new TenantSettingRequestDto(TenantSettingNames.AppendInstanceId, "not-a-bool"),
      cancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.ValidationFailed, result.ErrorCode);
  }

  [Fact]
  public async Task SetSetting_WhenInstanceIdIsWhitespace_RemovesExistingSetting()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();

    await using (var arrangeScope = _testApp.Services.CreateAsyncScope())
    {
      var db = arrangeScope.ServiceProvider.GetRequiredService<AppDb>();
      db.TenantSettings.Add(new TenantSetting
      {
        Name = TenantSettingNames.InstanceId,
        TenantId = tenant.Id,
        Value = "existing-instance"
      });
      await db.SaveChangesAsync(cancellationToken);
    }

    var result = await _tenantSettingsManager.SetSetting(
      tenant.Id,
      new TenantSettingRequestDto(TenantSettingNames.InstanceId, "   "),
      cancellationToken);

    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
    Assert.Null(result.Value.Value);

    await using var assertScope = _testApp.Services.CreateAsyncScope();
    var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDb>();
    var storedSetting = await assertDb.TenantSettings
      .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Name == TenantSettingNames.InstanceId, cancellationToken);

    Assert.Null(storedSetting);
  }

  [Fact]
  public async Task SetSetting_WhenInstanceIdUsesReservedDefaultValue_ReturnsValidationFailed()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();

    var result = await _tenantSettingsManager.SetSetting(
      tenant.Id,
      new TenantSettingRequestDto(TenantSettingNames.InstanceId, "DeFaUlT"),
      cancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.ValidationFailed, result.ErrorCode);
    Assert.Contains("reserved", result.Reason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task SetSetting_WhenInstanceIdValueIsInvalid_ReturnsValidationFailed()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();

    var result = await _tenantSettingsManager.SetSetting(
      tenant.Id,
      new TenantSettingRequestDto(TenantSettingNames.InstanceId, "bad id"),
      cancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.ValidationFailed, result.ErrorCode);
  }

  [Fact]
  public async Task SetSetting_WhenNotifyUserOnSessionStartValueIsInvalid_ReturnsValidationFailed()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();

    var result = await _tenantSettingsManager.SetSetting(
      tenant.Id,
      new TenantSettingRequestDto(TenantSettingNames.NotifyUserOnSessionStart, "not-a-bool"),
      cancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.ValidationFailed, result.ErrorCode);
  }

  [Fact]
  public async Task SetSetting_WhenSettingExists_UpdatesExistingValue()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();

    await _tenantSettingsManager.SetSetting(
      tenant.Id,
      new TenantSettingRequestDto(TenantSettingNames.AppendInstanceId, bool.TrueString),
      cancellationToken);

    var result = await _tenantSettingsManager.SetSetting(
      tenant.Id,
      new TenantSettingRequestDto(TenantSettingNames.AppendInstanceId, bool.FalseString),
      cancellationToken);

    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
    Assert.Equal(bool.FalseString, result.Value.Value);

    await using var assertScope = _testApp.Services.CreateAsyncScope();
    var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDb>();
    var storedSettings = await assertDb.TenantSettings
      .Where(x => x.TenantId == tenant.Id && x.Name == TenantSettingNames.AppendInstanceId)
      .ToListAsync(cancellationToken);

    Assert.Single(storedSettings);
    Assert.Equal(bool.FalseString, storedSettings[0].Value);
  }

  [Fact]
  public async Task SetSetting_WhenSettingIsValid_CreatesSetting()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var tenant = await _testApp.Services.CreateTestTenant();

    var result = await _tenantSettingsManager.SetSetting(
      tenant.Id,
      new TenantSettingRequestDto(TenantSettingNames.InstanceId, "server-alpha"),
      cancellationToken);

    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
    Assert.Equal("server-alpha", result.Value.Value);

    await using var assertScope = _testApp.Services.CreateAsyncScope();
    var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDb>();
    var storedSetting = await assertDb.TenantSettings
      .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Name == TenantSettingNames.InstanceId, cancellationToken);

    Assert.NotNull(storedSetting);
    Assert.Equal("server-alpha", storedSetting.Value);
  }

  [Fact]
  public async Task SetSetting_WhenTenantDoesNotExist_ReturnsNotFound()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var result = await _tenantSettingsManager.SetSetting(
      Guid.NewGuid(),
      new TenantSettingRequestDto(TenantSettingNames.InstanceId, "server-alpha"),
      cancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.NotFound, result.ErrorCode);
  }
}