using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Tests.TestingUtilities;
using ControlR.Web.Server.Services;
using Microsoft.Extensions.Time.Testing;
using Xunit.Abstractions;

namespace ControlR.Web.Server.Tests;


public class AgentInstallerKeyManagerTests
{
  private readonly FakeTimeProvider _timeProvider;
  private readonly AgentInstallerKeyManager _keyManager;

  public AgentInstallerKeyManagerTests(ITestOutputHelper testOutput)
  {
    _timeProvider = new FakeTimeProvider(DateTimeOffset.Now);
    var logger = new XunitLogger<AgentInstallerKeyManager>(testOutput);

    _keyManager = new AgentInstallerKeyManager(
      _timeProvider,
      logger);
  }

  [Fact]
  public async Task ValidateKey_WhenKeyDoesNotExist_Fails()
  {
    var validateResult = await _keyManager.ValidateKey("asdf");
    Assert.False(validateResult);
  }

  [Fact]
  public async Task ValidateKey_WhenUsageBasedKeyExists_Succeeds()
  {
    var key = await _keyManager.CreateKey(
      tenantId: Guid.NewGuid(),
      creatorId: Guid.NewGuid(),
      keyType: InstallerKeyType.UsageBased,
      allowedUses: 1,
      expiration: null);

    var validateResult = await _keyManager.ValidateKey(key.KeySecret);
    Assert.True(validateResult);
  }

  [Fact]
  public async Task ValidateKey_WhenTimeBasedKeyExistsAndNotExpired_Succeeds()
  {
    var key = await _keyManager.CreateKey(
      tenantId: Guid.NewGuid(),
      creatorId: Guid.NewGuid(),
      keyType: InstallerKeyType.TimeBased,
      allowedUses: null,
      expiration: _timeProvider.GetLocalNow().AddHours(1));

    var validateResult = await _keyManager.ValidateKey(key.KeySecret);

    Assert.True(validateResult);
  }

  [Fact]
  public async Task ValidateKey_WhenTimeBasedKeyExistsAndExpired_Fails()
  {
    var key = await _keyManager.CreateKey(
      tenantId: Guid.NewGuid(),
      creatorId: Guid.NewGuid(),
      keyType: InstallerKeyType.TimeBased,
      allowedUses: null,
      expiration: _timeProvider.GetLocalNow().AddHours(1));

    var validateResult = await _keyManager.ValidateKey(key.KeySecret);
    Assert.True(validateResult);

    // Advance time 50 minutes, in increments of 10 minutes.
    // Key should be valid at each point in time.
    for (var i = 0; i < 5; i++)
    {
      _timeProvider.Advance(TimeSpan.FromMinutes(10));
      validateResult = await _keyManager.ValidateKey(key.KeySecret);
      Assert.True(validateResult);
    }

    // Advance time 1 hour. Key should now be expired.
    _timeProvider.Advance(TimeSpan.FromHours(1));
    validateResult = await _keyManager.ValidateKey(key.KeySecret);
    Assert.False(validateResult);
  }

  [Theory]
  [InlineData(1)]
  [InlineData(5)]
  [InlineData(10)]
  public async Task ValidateKey_WhenUsageBasedKeyExistsAndUsedUp_Fails(uint allowedUses)
  {
    var key = await _keyManager.CreateKey(
      tenantId: Guid.NewGuid(),
      creatorId: Guid.NewGuid(),
      keyType: InstallerKeyType.UsageBased,
      allowedUses: allowedUses,
      expiration: null);

    for (var i = 0; i < allowedUses; i++)
    {
      var validateResult = await _keyManager.ValidateKey(key.KeySecret);
      Assert.True(validateResult);
    }

    var finalValidateResult = await _keyManager.ValidateKey(key.KeySecret);
    Assert.False(finalValidateResult);
  }
}
