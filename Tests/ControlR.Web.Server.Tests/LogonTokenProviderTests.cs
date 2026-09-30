using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services.LogonTokens;
using ControlR.Web.Server.Services.Settings;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class LogonTokenProviderTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task CreateTokenForExternal_RepeatWithSameCorrelationId_UpdatesDisplayNamePreference()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput, useInMemoryDatabase: false);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();
    var preferencesManager = scope.ServiceProvider.GetRequiredService<IUserPreferencesManager>();

    var tenant = await testApp.App.Services.CreateTestTenant();
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var userCorrelationId = $"test-{Guid.NewGuid():N}";

    var firstResult = await logonTokenProvider.CreateTokenForExternal(
      device.Id, tenant.Id, userCorrelationId,
      userDisplayName: "First Name",
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(firstResult.IsSuccess);

    var secondResult = await logonTokenProvider.CreateTokenForExternal(
      device.Id, tenant.Id, userCorrelationId,
      userDisplayName: "Second Name",
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(secondResult.IsSuccess);
    Assert.Equal(firstResult.Value.UserId, secondResult.Value.UserId);

    var preferences = await preferencesManager.GetAllPreferences(
      secondResult.Value.UserId, TestContext.Current.CancellationToken);

    Assert.Equal("Second Name", preferences.UserDisplayName);
  }

  [Fact]
  public async Task CreateTokenForExternal_WithExternalUser_CreatesUserAndToken()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var deviceId = Guid.NewGuid();
    var tenant = await testApp.App.Services.CreateTestTenant();
    var userCorrelationId = $"test-{Guid.NewGuid():N}";

    var result = await logonTokenProvider.CreateTokenForExternal(deviceId, tenant.Id, userCorrelationId, cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
    Assert.NotEmpty(result.Value.Token);
    Assert.Equal(deviceId, result.Value.DeviceId);
    Assert.Equal(tenant.Id, result.Value.TenantId);
    Assert.True(result.Value.ExpiresAt > DateTimeOffset.UtcNow);
  }

  [Fact]
  public async Task CreateTokenForExternal_WithUserDisplayName_SetsPreference()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();
    var preferencesManager = scope.ServiceProvider.GetRequiredService<IUserPreferencesManager>();

    var deviceId = Guid.NewGuid();
    var tenant = await testApp.App.Services.CreateTestTenant();
    var userCorrelationId = $"test-{Guid.NewGuid():N}";
    var userDisplayName = "Test Display Name";

    var createResult = await logonTokenProvider.CreateTokenForExternal(
      deviceId, tenant.Id, userCorrelationId,
      userDisplayName: userDisplayName,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(createResult.IsSuccess);

    var preferences = await preferencesManager.GetAllPreferences(
      createResult.Value.UserId, TestContext.Current.CancellationToken);

    Assert.Equal(userDisplayName, preferences.UserDisplayName);
  }

  [Fact]
  public async Task CreateToken_SessionExpirationMinutesDefaultsToDtoLimit()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var deviceId = Guid.NewGuid();
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);

    var createResult = await logonTokenProvider.CreateToken(deviceId, tenant.Id, user.Id, cancellationToken: TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess);

    var validateResult = await logonTokenProvider.ValidateToken(createResult.Value.Token, TestContext.Current.CancellationToken);

    Assert.True(validateResult.IsValid);
    Assert.Equal(DtoLimits.SessionExpirationMinutesDefault, validateResult.SessionExpirationMinutes);
  }

  [Fact]
  public async Task CreateToken_SessionExpirationMinutes_RoundTripsThroughValidation()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var deviceId = Guid.NewGuid();
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);

    var createResult = await logonTokenProvider.CreateToken(
      deviceId, tenant.Id, user.Id,
      sessionExpirationMinutes: 25,
      cancellationToken: TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess);

    var validateResult = await logonTokenProvider.ValidateToken(createResult.Value.Token, TestContext.Current.CancellationToken);

    Assert.True(validateResult.IsValid);
    Assert.Equal(25, validateResult.SessionExpirationMinutes);
  }

  [Fact]
  public async Task CreateToken_WithInvalidUserId_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var deviceId = Guid.NewGuid();
    var tenant = await testApp.App.Services.CreateTestTenant();
    var invalidUserId = Guid.NewGuid();

    var result = await logonTokenProvider.CreateToken(deviceId, tenant.Id, invalidUserId, cancellationToken: TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.NotFound, result.ErrorCode);
  }

  [Fact]
  public async Task CreateToken_WithoutSessionCorrelationId_ReturnsNull()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    var createResult = await logonTokenProvider.CreateToken(
      device.Id, tenant.Id, user.Id, cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(createResult.IsSuccess);
    Assert.Null(createResult.Value.SessionCorrelationId);

    var validationResult = await logonTokenProvider.ValidateAndConsumeToken(
      createResult.Value.Token, device.Id, TestContext.Current.CancellationToken);

    Assert.True(validationResult.IsValid);
    Assert.Null(validationResult.SessionCorrelationId);
  }

  [Fact]
  public async Task CreateToken_WithSessionCorrelationId_RoundTripsThroughValidation()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var sessionCorrelationId = $"session-{Guid.NewGuid():N}";

    var createResult = await logonTokenProvider.CreateToken(
      device.Id, tenant.Id, user.Id,
      sessionCorrelationId: sessionCorrelationId,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(createResult.IsSuccess);
    Assert.Equal(sessionCorrelationId, createResult.Value.SessionCorrelationId);

    var validationResult = await logonTokenProvider.ValidateAndConsumeToken(
      createResult.Value.Token, device.Id, TestContext.Current.CancellationToken);

    Assert.True(validationResult.IsValid);
    Assert.Equal(sessionCorrelationId, validationResult.SessionCorrelationId);
  }

  [Fact]
  public async Task CreateToken_WithValidInput_ReturnsSuccess()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var deviceId = Guid.NewGuid();
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);

    var result = await logonTokenProvider.CreateToken(deviceId, tenant.Id, user.Id, cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
    Assert.NotEmpty(result.Value.Token);
    Assert.Equal(deviceId, result.Value.DeviceId);
    Assert.Equal(tenant.Id, result.Value.TenantId);
    Assert.Equal(user.Id, result.Value.UserId);
    Assert.True(result.Value.ExpiresAt > DateTimeOffset.UtcNow);
  }

  [Fact]
  public async Task ValidateAndConsumeToken_WhenAlreadyConsumed_ReturnsFailure()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    var createResult = await logonTokenProvider.CreateToken(device.Id, tenant.Id, user.Id, cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(createResult.IsSuccess);
    var firstValidation = await logonTokenProvider.ValidateAndConsumeToken(createResult.Value.Token, device.Id, TestContext.Current.CancellationToken);

    var secondValidation = await logonTokenProvider.ValidateAndConsumeToken(createResult.Value.Token, device.Id, TestContext.Current.CancellationToken);

    Assert.True(firstValidation.IsValid);
    Assert.False(secondValidation.IsValid);
  }

  [Fact]
  public async Task ValidateAndConsumeToken_WhenConcurrentCalls_PreventsDoubleConsumption()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput, useInMemoryDatabase: false);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    var createResult = await logonTokenProvider.CreateToken(device.Id, tenant.Id, user.Id, cancellationToken: TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess);

    var token = createResult.Value.Token;

    var tasks = Enumerable.Range(0, 10)
      .Select(_ => logonTokenProvider.ValidateAndConsumeToken(token, device.Id, TestContext.Current.CancellationToken))
      .ToArray();

    var results = await Task.WhenAll(tasks);

    var successCount = results.Count(r => r.IsValid);
    var failureCount = results.Length - successCount;

    Assert.Equal(1, successCount);
    Assert.Equal(9, failureCount);
  }

  [Fact]
  public async Task ValidateToken_WhenExpired_ReturnsFailure()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var deviceId = Guid.NewGuid();
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var expirationMinutes = 15;

    var createResult = await logonTokenProvider.CreateToken(deviceId, tenant.Id, user.Id, expirationMinutes, cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(createResult.IsSuccess);
    testApp.TimeProvider.Advance(TimeSpan.FromMinutes(expirationMinutes + 1));

    var result = await logonTokenProvider.ValidateToken(createResult.Value.Token, TestContext.Current.CancellationToken);

    Assert.False(result.IsValid);
    Assert.Contains("expired", result.ErrorMessage);
  }

  [Fact]
  public async Task ValidateToken_WhenInvalid_ReturnsFailure()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var invalidToken = "invalid-token";

    var result = await logonTokenProvider.ValidateToken(invalidToken, TestContext.Current.CancellationToken);

    Assert.False(result.IsValid);
  }

  [Fact]
  public async Task ValidateToken_WhenValid_ReturnsSuccess()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var deviceId = Guid.NewGuid();
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);

    var createResult = await logonTokenProvider.CreateToken(deviceId, tenant.Id, user.Id, cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(createResult.IsSuccess);
    var validateResult = await logonTokenProvider.ValidateToken(createResult.Value.Token, TestContext.Current.CancellationToken);

    Assert.True(validateResult.IsValid);
    Assert.Equal(user.Id, validateResult.UserId);
    Assert.Equal(tenant.Id, validateResult.TenantId);
  }
}

