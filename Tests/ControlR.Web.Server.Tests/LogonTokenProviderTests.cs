using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services.LogonTokens;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class LogonTokenProviderTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

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
    Assert.False(result.Value.IsConsumed);
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
    Assert.False(result.Value.IsConsumed);
  }

  [Fact]
  public async Task ValidateAndConsumeToken_WhenAlreadyConsumed_ReturnsFailure()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var deviceId = Guid.NewGuid();
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);

    var createResult = await logonTokenProvider.CreateToken(deviceId, tenant.Id, user.Id, cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(createResult.IsSuccess);
    var firstValidation = await logonTokenProvider.ValidateAndConsumeToken(createResult.Value.Token, deviceId, TestContext.Current.CancellationToken);

    var secondValidation = await logonTokenProvider.ValidateAndConsumeToken(createResult.Value.Token, deviceId, TestContext.Current.CancellationToken);

    Assert.True(firstValidation.IsValid);
    Assert.False(secondValidation.IsValid);
    Assert.Contains("already been used", secondValidation.ErrorMessage);
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

    var createResult = await logonTokenProvider.CreateToken(deviceId, tenant.Id, user.Id, expirationMinutes, TestContext.Current.CancellationToken);

    Assert.True(createResult.IsSuccess);
    testApp.TimeProvider.Advance(TimeSpan.FromMinutes(expirationMinutes + 1));

    var result = await logonTokenProvider.ValidateToken(createResult.Value.Token, TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Contains("expired", result.Reason);
  }

  [Fact]
  public async Task ValidateToken_WhenInvalid_ReturnsFailure()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var invalidToken = "invalid-token";

    var result = await logonTokenProvider.ValidateToken(invalidToken, TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
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

    Assert.True(validateResult.IsSuccess);
    Assert.Equal(user.Id, validateResult.Value.UserId);
    Assert.Equal(tenant.Id, validateResult.Value.TenantId);
  }
}

