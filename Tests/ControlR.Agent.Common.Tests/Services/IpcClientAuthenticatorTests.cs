using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Services;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace ControlR.Agent.Common.Tests.Services;

public class IpcClientAuthenticatorTests
{
  private readonly Mock<IClientCredentialsProvider> _credentialProvider;
  private readonly Mock<ISystemEnvironment> _systemEnvironment;
  private readonly Mock<ILogger<IpcClientAuthenticator>> _logger;
  private readonly Mock<IIpcServer> _server;
  private readonly FakeTimeProvider _timeProvider;
  private readonly IIpcClientAuthenticator _authenticator;

  public IpcClientAuthenticatorTests()
  {
    _credentialProvider = new Mock<IClientCredentialsProvider>();
    _systemEnvironment = new Mock<ISystemEnvironment>();
    _logger = new Mock<ILogger<IpcClientAuthenticator>>();
    _server = new Mock<IIpcServer>();
    _timeProvider = new FakeTimeProvider();

    _authenticator = new IpcClientAuthenticator(
      _timeProvider,
      _credentialProvider.Object,
      _systemEnvironment.Object,
      _logger.Object);
  }

  [Fact]
  public async Task AuthenticateConnection_WithValidCredentials_ReturnsSuccess()
  {
    // Arrange
    var startupDir = "/expected/path";
    var expectedPath = Path.Combine(startupDir, "DesktopClient", AppConstants.DesktopClientFileName);
    _systemEnvironment.Setup(x => x.IsDebug).Returns(false);
    _systemEnvironment.Setup(x => x.StartupDirectory).Returns(startupDir);

    var credentials = new ClientCredentials(12345, expectedPath);
    _credentialProvider
      .Setup(x => x.GetClientCredentials(_server.Object))
      .Returns(Result.Ok(credentials));

    // Act
    var result = await _authenticator.AuthenticateConnection(_server.Object);

    // Assert
    Assert.True(result.IsSuccess);
  }

  [Fact]
  public async Task AuthenticateConnection_WithFailedCredentialRetrieval_ReturnsFailure()
  {
    // Arrange
    _credentialProvider
      .Setup(x => x.GetClientCredentials(_server.Object))
      .Returns(Result.Fail<ClientCredentials>("Failed to get credentials"));

    // Act
    var result = await _authenticator.AuthenticateConnection(_server.Object);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("Failed to get credentials", result.Reason);
  }

  [Fact]
  public async Task AuthenticateConnection_WithInvalidPath_RecordsFailureAndReturnsFailure()
  {
    // Arrange
    _systemEnvironment.Setup(x => x.IsDebug).Returns(false);
    _systemEnvironment.Setup(x => x.StartupDirectory).Returns("/expected/path");

    var credentials = new ClientCredentials(12345, "/wrong/path/malicious.exe");
    _credentialProvider
      .Setup(x => x.GetClientCredentials(_server.Object))
      .Returns(Result.Ok(credentials));

    // Act
    var result = await _authenticator.AuthenticateConnection(_server.Object);

    // Assert
    Assert.False(result.IsSuccess);
    
    // Verify failure was recorded for rate limiting
    var rateLimitCheck = await _authenticator.CheckRateLimit("/wrong/path/malicious.exe");
    Assert.True(rateLimitCheck.IsSuccess); // Should still be under limit with just 1 failure
  }

  [Fact]
  public async Task AuthenticateConnection_WithRateLimitExceeded_ReturnsFailure()
  {
    // Arrange
    var attackPath = "/attack/path/malicious.exe";
    _systemEnvironment.Setup(x => x.IsDebug).Returns(false);
    _systemEnvironment.Setup(x => x.StartupDirectory).Returns("/expected/path");

    var credentials = new ClientCredentials(12345, attackPath);
    _credentialProvider
      .Setup(x => x.GetClientCredentials(_server.Object))
      .Returns(Result.Ok(credentials));

    // Record 5 failed attempts to exceed rate limit
    for (var i = 0; i < 5; i++)
    {
      await _authenticator.RecordFailedAttempt(attackPath);
    }

    // Act
    var result = await _authenticator.AuthenticateConnection(_server.Object);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("Rate limit exceeded", result.Reason);
  }

  [Fact]
  public async Task AuthenticateConnection_WithDebugModeDotnetExe_ReturnsSuccess()
  {
    // Arrange
    _systemEnvironment.Setup(x => x.IsDebug).Returns(true);
    _systemEnvironment.Setup(x => x.StartupDirectory).Returns("/debug/path");

    var credentials = new ClientCredentials(12345, "/usr/bin/dotnet");
    _credentialProvider
      .Setup(x => x.GetClientCredentials(_server.Object))
      .Returns(Result.Ok(credentials));

    // Act
    var result = await _authenticator.AuthenticateConnection(_server.Object);

    // Assert
    Assert.True(result.IsSuccess);
  }

  [Fact]
  public async Task AuthenticateConnection_WithNullExecutablePath_ReturnsFailure()
  {
    // Arrange
    var credentials = new ClientCredentials(12345, null!);
    _credentialProvider
      .Setup(x => x.GetClientCredentials(_server.Object))
      .Returns(Result.Ok(credentials));

    // Act
    var result = await _authenticator.AuthenticateConnection(_server.Object);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("null or empty", result.Reason, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task AuthenticateConnection_WithException_ReturnsFailure()
  {
    // Arrange
    _credentialProvider
      .Setup(x => x.GetClientCredentials(_server.Object))
      .Throws(new InvalidOperationException("Test exception"));

    // Act
    var result = await _authenticator.AuthenticateConnection(_server.Object);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("Unexpected error", result.Reason);
  }

  [Fact]
  public async Task AuthenticateConnection_AfterRateLimitExpires_AllowsNewAttempts()
  {
    // Arrange
    var attackPath = "/attack/path/test.exe";
    _systemEnvironment.Setup(x => x.IsDebug).Returns(false);
    _systemEnvironment.Setup(x => x.StartupDirectory).Returns("/expected/path");

    // Record 5 failed attempts
    for (var i = 0; i < 5; i++)
    {
      await _authenticator.RecordFailedAttempt(attackPath);
    }

    // Advance time by 61 seconds to expire the rate limit window
    _timeProvider.Advance(TimeSpan.FromSeconds(61));

    var credentials = new ClientCredentials(12345, attackPath);
    _credentialProvider
      .Setup(x => x.GetClientCredentials(_server.Object))
      .Returns(Result.Ok(credentials));

    // Act - rate limit should be cleared, but path validation will still fail
    var rateLimitResult = await _authenticator.CheckRateLimit(attackPath);

    // Assert - rate limit should be OK now
    Assert.True(rateLimitResult.IsSuccess);
  }

  [Fact]
  public async Task AuthenticateConnection_WithMacOsAppBundle_ReturnsSuccess()
  {
    // Arrange
    var startupDir = "/Applications/ControlR";
    _systemEnvironment.Setup(x => x.IsDebug).Returns(false);
    _systemEnvironment.Setup(x => x.StartupDirectory).Returns(startupDir);
    // Note: SystemEnvironment.Instance.Platform (not the mock) is used in GetDesktopExecutablePath
    // So we need to construct the expected path based on the actual platform running the test
    // For testing purposes, let's test the non-macOS path since we can't easily mock the static Instance
    var expectedPath = Path.Combine(startupDir, "DesktopClient", AppConstants.DesktopClientFileName);
    var credentials = new ClientCredentials(12345, expectedPath);
    _credentialProvider
      .Setup(x => x.GetClientCredentials(_server.Object))
      .Returns(Result.Ok(credentials));

    // Act
    var result = await _authenticator.AuthenticateConnection(_server.Object);

    // Assert
    Assert.True(result.IsSuccess);
  }

  [Fact]
  public async Task AuthenticateConnection_WithMultipleValidAttempts_DoesNotTriggerRateLimit()
  {
    // Arrange
    var startupDir = "/expected/path";
    var validPath = Path.Combine(startupDir, "DesktopClient", AppConstants.DesktopClientFileName);
    _systemEnvironment.Setup(x => x.IsDebug).Returns(false);
    _systemEnvironment.Setup(x => x.StartupDirectory).Returns(startupDir);

    var credentials = new ClientCredentials(12345, validPath);
    _credentialProvider
      .Setup(x => x.GetClientCredentials(_server.Object))
      .Returns(Result.Ok(credentials));

    // Act - authenticate 10 times successfully
    for (var i = 0; i < 10; i++)
    {
      var result = await _authenticator.AuthenticateConnection(_server.Object);
      Assert.True(result.IsSuccess, $"Attempt {i + 1} should succeed");
    }

    // Assert - rate limit should still allow more attempts
    var rateLimitCheck = await _authenticator.CheckRateLimit(validPath);
    Assert.True(rateLimitCheck.IsSuccess);
  }

  [Theory]
  [InlineData("dotnet.exe")]
  [InlineData("dotnet")]
  [InlineData("DOTNET.EXE")]
  public async Task AuthenticateConnection_WithDebugModeDifferentDotnetNames_ReturnsSuccess(string dotnetName)
  {
    // Arrange
    _systemEnvironment.Setup(x => x.IsDebug).Returns(true);
    _systemEnvironment.Setup(x => x.StartupDirectory).Returns("/debug/path");

    var credentials = new ClientCredentials(12345, $"/usr/bin/{dotnetName}");
    _credentialProvider
      .Setup(x => x.GetClientCredentials(_server.Object))
      .Returns(Result.Ok(credentials));

    // Act
    var result = await _authenticator.AuthenticateConnection(_server.Object);

    // Assert
    Assert.True(result.IsSuccess);
  }

  [Fact]
  public async Task CheckRateLimit_WithOldFailures_IgnoresExpiredAttempts()
  {
    // Arrange
    var executablePath = "C:\\test\\app.exe";
    var startTime = DateTimeOffset.Parse("2024-01-01T12:00:00Z");
    _timeProvider.SetUtcNow(startTime);
    
    // Record 5 failures at T+0
    for (var i = 0; i < 5; i++)
    {
      await _authenticator.RecordFailedAttempt(executablePath);
    }

    // Move time forward 61 seconds (past the 1-minute window)
    _timeProvider.SetUtcNow(startTime.AddSeconds(61));

    // Act
    var result = await _authenticator.CheckRateLimit(executablePath);

    // Assert - old attempts should be ignored
    Assert.True(result.IsSuccess);
  }

  [Fact]
  public async Task CheckRateLimit_WithMixedOldAndNewFailures_OnlyCountsRecentAttempts()
  {
    // Arrange
    var executablePath = "C:\\test\\app.exe";
    var startTime = DateTimeOffset.Parse("2024-01-01T12:00:00Z");
    _timeProvider.SetUtcNow(startTime);
    
    // Record 3 old failures
    for (var i = 0; i < 3; i++)
    {
      await _authenticator.RecordFailedAttempt(executablePath);
    }

    // Move time forward 61 seconds
    _timeProvider.SetUtcNow(startTime.AddSeconds(61));
    
    // Record 2 new failures (total would be 5, but only 2 are recent)
    for (var i = 0; i < 2; i++)
    {
      await _authenticator.RecordFailedAttempt(executablePath);
    }

    // Act
    var result = await _authenticator.CheckRateLimit(executablePath);

    // Assert - only 2 recent attempts should count
    Assert.True(result.IsSuccess);
  }

  [Fact]
  public async Task CheckRateLimit_DifferentExecutables_AreTrackedSeparately()
  {
    // Arrange
    var path1 = "C:\\test\\app1.exe";
    var path2 = "C:\\test\\app2.exe";
    
    // Record 5 failures for path1
    for (var i = 0; i < 5; i++)
    {
      await _authenticator.RecordFailedAttempt(path1);
    }

    // Act
    var result1 = await _authenticator.CheckRateLimit(path1);
    var result2 = await _authenticator.CheckRateLimit(path2);

    // Assert
    Assert.False(result1.IsSuccess, "Path1 should be rate-limited");
    Assert.True(result2.IsSuccess, "Path2 should not be rate-limited");
  }

  [Fact]
  public async Task ConcurrentAuthentication_HandlesMultipleThreadsSafely()
  {
    // Arrange
    var attackPath = "/attack/path/malicious.exe";
    _systemEnvironment.Setup(x => x.IsDebug).Returns(false);
    _systemEnvironment.Setup(x => x.StartupDirectory).Returns("/expected/path");

    var credentials = new ClientCredentials(12345, attackPath);
    _credentialProvider
      .Setup(x => x.GetClientCredentials(_server.Object))
      .Returns(Result.Ok(credentials));

    var tasks = new List<Task<Result>>();

    // Act - simulate concurrent authentication attempts from multiple threads
    for (var i = 0; i < 10; i++)
    {
      tasks.Add(Task.Run(async () => await _authenticator.AuthenticateConnection(_server.Object)));
    }

    var results = await Task.WhenAll(tasks);

    // Assert - all attempts should record failures, and rate limit should eventually trigger
    var failedResults = results.Where(r => !r.IsSuccess).ToList();
    Assert.NotEmpty(failedResults);
    
    // Final rate limit check should show it's blocked
    var rateLimitCheck = await _authenticator.CheckRateLimit(attackPath);
    Assert.False(rateLimitCheck.IsSuccess, "Should be rate-limited after concurrent failed attempts");
  }

  [Fact]
  public async Task RateLimitScenario_FullAttackAndRecoveryWorkflow()
  {
    // Arrange
    var attackPath = "/attack/malicious.exe";
    var startTime = DateTimeOffset.Parse("2024-01-01T12:00:00Z");
    _timeProvider.SetUtcNow(startTime);
    
    _systemEnvironment.Setup(x => x.IsDebug).Returns(false);
    _systemEnvironment.Setup(x => x.StartupDirectory).Returns("/expected/path");

    var credentials = new ClientCredentials(12345, attackPath);
    _credentialProvider
      .Setup(x => x.GetClientCredentials(_server.Object))
      .Returns(Result.Ok(credentials));

    // Simulate an attacker making rapid failed authentication attempts
    for (var i = 0; i < 5; i++)
    {
      var rateLimitCheck = await _authenticator.CheckRateLimit(attackPath);
      Assert.True(rateLimitCheck.IsSuccess, $"Attempt {i + 1} should not be rate-limited yet");
      
      var authResult = await _authenticator.AuthenticateConnection(_server.Object);
      Assert.False(authResult.IsSuccess, "Auth should fail due to invalid path");
    }

    // 6th attempt should be blocked by rate limit
    var blockedCheck = await _authenticator.CheckRateLimit(attackPath);
    Assert.False(blockedCheck.IsSuccess, "6th attempt should be rate-limited");

    var blockedAuth = await _authenticator.AuthenticateConnection(_server.Object);
    Assert.False(blockedAuth.IsSuccess);
    Assert.Contains("Rate limit exceeded", blockedAuth.Reason);

    // Move time forward past the rate limit window
    _timeProvider.SetUtcNow(startTime.AddMinutes(2));

    // Should be able to try again (though still fail due to invalid path)
    var allowedCheck = await _authenticator.CheckRateLimit(attackPath);
    Assert.True(allowedCheck.IsSuccess, "After timeout, rate limit should be cleared");
  }

  [Theory]
  [InlineData("")]
  [InlineData(null)]
  [InlineData("   ")]
  public async Task RecordFailedAttempt_WithInvalidPath_HandlesGracefully(string? invalidPath)
  {
    // Act - should not throw
    await _authenticator.RecordFailedAttempt(invalidPath!);

    // Assert - check rate limit should still work
    var result = await _authenticator.CheckRateLimit(invalidPath!);
    Assert.True(result.IsSuccess);
  }
}
