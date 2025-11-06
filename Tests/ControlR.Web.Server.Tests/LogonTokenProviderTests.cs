using ControlR.Web.Server.Services.LogonTokens;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace ControlR.Web.Server.Tests;

public class LogonTokenProviderTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task CreateTokenAsync_CreatesTokenWithCorrectProperties()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var deviceId = Guid.NewGuid();
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);

    // Act
    var result = await logonTokenProvider.CreateTokenAsync(deviceId, tenant.Id, user.Id);
    
    // Assert
    Assert.NotNull(result);
    Assert.NotEmpty(result.Token);
    Assert.Equal(deviceId, result.DeviceId);
    Assert.Equal(tenant.Id, result.TenantId);
    Assert.Equal(user.Id, result.UserId);
    Assert.True(result.ExpiresAt > DateTimeOffset.UtcNow);
    Assert.False(result.IsConsumed);
  }

  [Fact]
  public async Task CreateTokenAsync_ThrowsForInvalidUser()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var deviceId = Guid.NewGuid();
    var tenant = await testApp.App.Services.CreateTestTenant();
    var invalidUserId = Guid.NewGuid();

    // Act & Assert
    await Assert.ThrowsAsync<InvalidOperationException>(async () =>
      await logonTokenProvider.CreateTokenAsync(deviceId, tenant.Id, invalidUserId));
  }

  [Fact]
  public async Task ValidateAndConsumeTokenAsync_FailsWhenTokenUsedSecondTime()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var deviceId = Guid.NewGuid();
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);

    // Create a token
    var token = await logonTokenProvider.CreateTokenAsync(deviceId, tenant.Id, user.Id);
    
    // Act - First consumption should succeed
    var firstValidation = await logonTokenProvider.ValidateAndConsumeTokenAsync(token.Token, deviceId);
    
    // Act - Second consumption should fail
    var secondValidation = await logonTokenProvider.ValidateAndConsumeTokenAsync(token.Token, deviceId);
    
    // Assert
    Assert.True(firstValidation.IsValid);
    Assert.False(secondValidation.IsValid);
    Assert.Contains("already been used", secondValidation.ErrorMessage);
  }

  [Fact]
  public async Task ValidateTokenAsync_FailsWhenTokenExpired()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var deviceId = Guid.NewGuid();
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);
    var expirationMinutes = 15;

    // Create a token that will expire soon
    var token = await logonTokenProvider.CreateTokenAsync(deviceId, tenant.Id, user.Id, expirationMinutes);
    
    // Act - Advance time past expiration
    testApp.TimeProvider.Advance(TimeSpan.FromMinutes(expirationMinutes + 1));
    
    var result = await logonTokenProvider.ValidateTokenAsync(token.Token);
    
    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("expired", result.Reason);
  }

  [Fact]
  public async Task ValidateTokenAsync_ReturnsFailureForInvalidToken()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var invalidToken = "invalid-token";

    // Act
    var result = await logonTokenProvider.ValidateTokenAsync(invalidToken);
    
    // Assert
    Assert.False(result.IsSuccess);
  }

  [Fact]
  public async Task ValidateTokenAsync_ReturnsValidResultForValidToken()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.App.Services.CreateScope();
    var logonTokenProvider = scope.ServiceProvider.GetRequiredService<ILogonTokenProvider>();

    var deviceId = Guid.NewGuid();
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id);

    // Create a token
    var token = await logonTokenProvider.CreateTokenAsync(deviceId, tenant.Id, user.Id);
    
    // Act
    var validateResult = await logonTokenProvider.ValidateTokenAsync(token.Token);
    
    // Assert
    Assert.True(validateResult.IsSuccess);
    Assert.Equal(user.Id, validateResult.Value.UserId);
    Assert.Equal(tenant.Id, validateResult.Value.TenantId);
  }
}

