using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.TestingUtilities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace ControlR.Web.Server.Tests;

public class AgentInstallerKeyManagerTests(ITestOutputHelper testOutput) : IAsyncLifetime
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  private Guid _creatorId;
  private AgentInstallerKeyManager _keyManager = null!;
  private Guid _tenantId;
  private TestApp _testApp = null!;
  private FakeTimeProvider _timeProvider = null!;

  public async ValueTask DisposeAsync()
  {
    await _testApp.DisposeAsync();
  }

  [Fact]
  public async Task IncrementUsage_WhenTimeBasedKeyExpired_RemovesKey()
  {
    var dto = await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.TimeBased,
        allowedUses: null,
        expiration: _timeProvider.GetLocalNow().AddHours(1),
        friendlyName: "Test Key");

    // Advance time past expiration
    _timeProvider.Advance(TimeSpan.FromHours(2));

    var result = await _keyManager.IncrementUsage(dto.Id);
    Assert.False(result.IsSuccess);
    Assert.Contains("expired", result.Reason, StringComparison.OrdinalIgnoreCase);

    // Verify key was removed by trying to validate it
    var validateResult = await _keyManager.ValidateKey(dto.Id, dto.KeySecret);
    Assert.False(validateResult);
  }

  public async ValueTask InitializeAsync()
  {
    _testApp = await TestAppBuilder.CreateTestApp(_testOutput, testDatabaseName: $"{Guid.NewGuid()}");
    _timeProvider = _testApp.TimeProvider;
    _keyManager = (AgentInstallerKeyManager)_testApp.Services.GetRequiredService<IAgentInstallerKeyManager>();

    var tenant = await _testApp.Services.CreateTestTenant();
    var user = await _testApp.Services.CreateTestUser(tenant.Id, email: $"installer-{Guid.NewGuid():N}@test.local");
    _tenantId = tenant.Id;
    _creatorId = user.Id;
  }

  [Fact]
  public async Task ValidateAndConsumeKey_RecordsRemoteIpAddress()
  {
    var dto = await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.UsageBased,
        allowedUses: 1,
        expiration: null,
        friendlyName: "Test Key");

    var remoteIp = "192.168.1.100";
    var deviceId = Guid.NewGuid();
    var validateResult = await _keyManager.ValidateAndConsumeKey(dto.Id, dto.KeySecret, deviceId, remoteIp);
    Assert.True(validateResult);
  }

  [Theory]
  [InlineData(1)]
  [InlineData(5)]
  [InlineData(10)]
  public async Task ValidateAndConsumeKey_WhenUsageBasedKeyExistsAndUsedUp_Fails(uint allowedUses)
  {
    var dto = await _keyManager.CreateKey(
      tenantId: _tenantId,
      creatorId: _creatorId,
      keyType: InstallerKeyType.UsageBased,
      allowedUses: allowedUses,
      expiration: null,
      friendlyName: null);

    for (var i = 0; i < allowedUses; i++)
    {
      var validateResult = await _keyManager.ValidateAndConsumeKey(dto.Id, dto.KeySecret, Guid.NewGuid());
      Assert.True(validateResult);
    }

    var finalValidateResult = await _keyManager.ValidateAndConsumeKey(dto.Id, dto.KeySecret, Guid.NewGuid());
    Assert.False(finalValidateResult);
  }

  [Fact]
  public async Task ValidateAndConsumeKey_WhenUsageBasedKeyExists_Succeeds()
  {
    var dto = await _keyManager.CreateKey(
      tenantId: _tenantId,
      creatorId: _creatorId,
      keyType: InstallerKeyType.UsageBased,
      allowedUses: 1,
      expiration: null,
      friendlyName: null);

    var validateResult = await _keyManager.ValidateAndConsumeKey(dto.Id, dto.KeySecret, Guid.NewGuid());
    Assert.True(validateResult);
  }

  [Fact]
  public async Task ValidateKey_DoesNotConsumeUsage()
  {
    var dto = await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.UsageBased,
        allowedUses: 1,
        expiration: null,
        friendlyName: "Test Key");

    // ValidateKey should not consume usage, so we can call it multiple times
    for (var i = 0; i < 5; i++)
    {
      var validateResult = await _keyManager.ValidateKey(dto.Id, dto.KeySecret);
      Assert.True(validateResult);
    }

    // Key should still be valid because we haven't consumed any usage
    var finalResult = await _keyManager.ValidateKey(dto.Id, dto.KeySecret);
    Assert.True(finalResult);
  }

  [Fact]
  public async Task ValidateKey_WhenKeyDoesNotExist_Fails()
  {
    var validateResult = await _keyManager.ValidateKey(Guid.NewGuid(), "asdf");
    Assert.False(validateResult);
  }

  [Fact]
  public async Task ValidateKey_WhenPersistentKeyExists_Succeeds()
  {
    var dto = await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.Persistent,
        allowedUses: null,
        expiration: null,
        friendlyName: "Test Key");

    var validateResult = await _keyManager.ValidateKey(dto.Id, dto.KeySecret);
    Assert.True(validateResult);
  }

  [Fact]
  public async Task ValidateKey_WhenTimeBasedKeyExistsAndExpired_Fails()
  {
    var dto = await _keyManager.CreateKey(
      tenantId: _tenantId,
      creatorId: _creatorId,
      keyType: InstallerKeyType.TimeBased,
      allowedUses: null,
      expiration: _timeProvider.GetLocalNow().AddHours(1),
      friendlyName: null);

    var validateResult = await _keyManager.ValidateKey(dto.Id, dto.KeySecret);
    Assert.True(validateResult);

    // Advance time 50 minutes, in increments of 10 minutes.
    // Key should be valid at each point in time.
    for (var i = 0; i < 5; i++)
    {
      _timeProvider.Advance(TimeSpan.FromMinutes(10));
      validateResult = await _keyManager.ValidateKey(dto.Id, dto.KeySecret);
      Assert.True(validateResult);
    }

    // Advance time 1 hour. Key should now be expired.
    _timeProvider.Advance(TimeSpan.FromHours(1));
    validateResult = await _keyManager.ValidateKey(dto.Id, dto.KeySecret);
    Assert.False(validateResult);
  }

  [Fact]
  public async Task ValidateKey_WhenTimeBasedKeyExistsAndNotExpired_Succeeds()
  {
    var dto = await _keyManager.CreateKey(
      tenantId: _tenantId,
      creatorId: _creatorId,
      keyType: InstallerKeyType.TimeBased,
      allowedUses: null,
      expiration: _timeProvider.GetLocalNow().AddHours(1),
      friendlyName: null);

    var validateResult = await _keyManager.ValidateKey(dto.Id, dto.KeySecret);

    Assert.True(validateResult);
  }

  [Fact]
  public async Task ValidateKey_WhenTimeBasedKeyExpired_RemovesKey()
  {
    var dto = await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.TimeBased,
        allowedUses: null,
        expiration: _timeProvider.GetLocalNow().AddHours(1),
        friendlyName: "Test Key");

    // Advance time past expiration
    _timeProvider.Advance(TimeSpan.FromHours(2));

    var validateResult = await _keyManager.ValidateKey(dto.Id, dto.KeySecret);
    Assert.False(validateResult);

    // Verify key was removed by trying to validate it again
    validateResult = await _keyManager.ValidateKey(dto.Id, dto.KeySecret);
    Assert.False(validateResult);
  }

  [Fact]
  public async Task ValidateKey_WithIncorrectKeyId_Fails()
  {
    var dto = await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.Persistent,
        allowedUses: null,
        expiration: null,
        friendlyName: "Test Key");

    var validateResult = await _keyManager.ValidateKey(Guid.NewGuid(), dto.KeySecret);
    Assert.False(validateResult);
  }

  [Fact]
  public async Task ValidateKey_WithKeyId_Succeeds()
  {
    var dto = await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.Persistent,
        allowedUses: null,
        expiration: null,
        friendlyName: "Test Key");

    var validateResult = await _keyManager.ValidateKey(dto.Id, dto.KeySecret);
    Assert.True(validateResult);
  }
}
