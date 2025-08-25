using ControlR.Libraries.Shared.Constants;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Services.LogonTokens;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit.Abstractions;

namespace ControlR.Web.Server.Tests;

public class LogonTokenProviderTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task CreateTemporaryUserAsync_CreatesUserWithCorrectProperties()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var logonTokenProvider = testApp.App.Services.GetRequiredService<ILogonTokenProvider>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();
    
    var deviceId = Guid.NewGuid();
    var displayName = "Test Device User";
    
    // Act
    var result = await logonTokenProvider.CreateTemporaryUserAsync(deviceId, displayName);
    
    // Assert
    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);

    var user = await db.Users
      .Include(u => u.UserPreferences)
      .FirstOrDefaultAsync(u => u.Id == result.Value.UserId);
    
    Assert.NotNull(user);
    Assert.True(user.IsTemporary);
    Assert.NotNull(user.TemporaryUserExpiresAt);
    Assert.True(user.TemporaryUserExpiresAt > DateTimeOffset.UtcNow);

    var displayNamePreference = user!.UserPreferences!
      .FirstOrDefault(p => p.Name == UserPreferenceNames.UserDisplayName);
    
    Assert.NotNull(displayNamePreference);
    Assert.Equal(displayName, displayNamePreference.Value);
  }

  [Fact]
  public async Task ValidateTokenAsync_ReturnsValidResultForValidToken()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var logonTokenProvider = testApp.App.Services.GetRequiredService<ILogonTokenProvider>();
    
    var deviceId = Guid.NewGuid();
    var displayName = "Test Device User";
    
    // Create a temporary user and token
    var createResult = await logonTokenProvider.CreateTemporaryUserAsync(deviceId, displayName);
    Assert.True(createResult.IsSuccess);
    
    // Act
    var validateResult = await logonTokenProvider.ValidateTokenAsync(createResult.Value.Token);
    
    // Assert
    Assert.True(validateResult.IsSuccess);
    Assert.Equal(createResult.Value.UserId, validateResult.Value.UserId);
  }

  [Fact]
  public async Task ValidateTokenAsync_ReturnsFailureForInvalidToken()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var logonTokenProvider = testApp.App.Services.GetRequiredService<ILogonTokenProvider>();
    
    var invalidToken = "invalid-token";
    
    // Act
    var result = await logonTokenProvider.ValidateTokenAsync(invalidToken);
    
    // Assert
    Assert.False(result.IsSuccess);
  }

  [Fact]
  public async Task ValidateAndConsumeTokenAsync_FailsWhenTokenUsedSecondTime()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var logonTokenProvider = testApp.App.Services.GetRequiredService<ILogonTokenProvider>();
    
    var deviceId = Guid.NewGuid();
    var displayName = "Test Device User";
    
    // Create a temporary user and token
    var createResult = await logonTokenProvider.CreateTemporaryUserAsync(deviceId, displayName);
    Assert.True(createResult.IsSuccess);
    var token = createResult.Value.Token;
    
    // Act - First consumption should succeed
    var firstValidation = await logonTokenProvider.ValidateAndConsumeTokenAsync(token, deviceId);
    
    // Act - Second consumption should fail
    var secondValidation = await logonTokenProvider.ValidateAndConsumeTokenAsync(token, deviceId);
    
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
    var logonTokenProvider = testApp.App.Services.GetRequiredService<ILogonTokenProvider>();
    
    var deviceId = Guid.NewGuid();
    var tenantId = Guid.Empty; // System tenant
    var expirationMinutes = 15;
    
    // Create a token that will expire soon
    var token = await logonTokenProvider.CreateTokenAsync(
      deviceId, 
      tenantId, 
      expirationMinutes,
      userIdentifier: Guid.NewGuid().ToString(),
      displayName: "Test User");
    
    // Act - Advance time past expiration
    testApp.TimeProvider.Advance(TimeSpan.FromMinutes(expirationMinutes + 1));
    
    var result = await logonTokenProvider.ValidateTokenAsync(token.Token);
    
    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("expired", result.Reason);
  }
}

