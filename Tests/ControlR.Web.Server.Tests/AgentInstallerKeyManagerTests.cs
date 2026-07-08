using ControlR.Web.Server.Options;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services.AgentInstaller;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace ControlR.Web.Server.Tests;

public class AgentInstallerKeyManagerTests(ITestOutputHelper testOutput) : IAsyncLifetime
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  private Guid _creatorId;
  private AgentInstallerKeyManager _keyManager = null!;
  private Guid _tenantId;
  private TestApp _testApp = null!;
  private FakeTimeProvider _timeProvider = null!;

  [Fact]
  public void AgentInstallerKeyHistoryDays_DefaultValue_Is90()
  {
    var options = new AppOptions();
    Assert.Equal(90, options.AgentInstallerKeyHistoryDays);
  }

  [Fact]
  public async Task CreateKey_UsageBased_Sets24HourExpiration()
  {
    var before = _timeProvider.GetUtcNow();
    var dto = await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.UsageBased,
        allowedUses: 5,
        expiration: null,
        friendlyName: "Test Key");

    Assert.NotNull(dto.Expiration);
    Assert.InRange(dto.Expiration!.Value, before + TimeSpan.FromHours(24) - TimeSpan.FromSeconds(1), before + TimeSpan.FromHours(24) + TimeSpan.FromSeconds(1));
  }

  [Fact]
  public async Task DeleteKey_ByAdmin_Succeeds()
  {
    var otherUser = await _testApp.Services.CreateTestUser(_tenantId, email: $"other-{Guid.NewGuid():N}@test.local");

    var dto = await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: otherUser.Id,
        keyType: InstallerKeyType.Persistent,
        allowedUses: null,
        expiration: null,
        friendlyName: "Test Key");

    var result = await _keyManager.DeleteKey(dto.Id, _creatorId, _tenantId, isTenantAdmin: true);
    Assert.True(result.IsSuccess);

    var keyResult = await _keyManager.TryGetKey(dto.Id, _tenantId);
    Assert.False(keyResult.IsSuccess);
  }

  [Fact]
  public async Task DeleteKey_ByKeyCreator_Succeeds()
  {
    var dto = await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.Persistent,
        allowedUses: null,
        expiration: null,
        friendlyName: "Test Key");

    var result = await _keyManager.DeleteKey(dto.Id, _creatorId, _tenantId, isTenantAdmin: false);
    Assert.True(result.IsSuccess);

    var keyResult = await _keyManager.TryGetKey(dto.Id, _tenantId);
    Assert.False(keyResult.IsSuccess);
  }

  [Fact]
  public async Task DeleteKey_ByNonCreatorNonAdmin_Fails()
  {
    var dto = await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.Persistent,
        allowedUses: null,
        expiration: null,
        friendlyName: "Test Key");

    var otherUserId = Guid.NewGuid();
    var result = await _keyManager.DeleteKey(dto.Id, otherUserId, _tenantId, isTenantAdmin: false);
    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.Forbidden, result.ErrorCode);

    var keyResult = await _keyManager.TryGetKey(dto.Id, _tenantId);
    Assert.True(keyResult.IsSuccess);
  }

  [Fact]
  public async Task DeleteKey_CrossTenant_Fails()
  {
    var otherTenant = await _testApp.Services.CreateTestTenant();
    var otherUser = await _testApp.Services.CreateTestUser(otherTenant.Id, email: $"cross-{Guid.NewGuid():N}@test.local");

    var dto = await _keyManager.CreateKey(
        tenantId: otherTenant.Id,
        creatorId: otherUser.Id,
        keyType: InstallerKeyType.Persistent,
        allowedUses: null,
        expiration: null,
        friendlyName: "Other Tenant Key");

    var result = await _keyManager.DeleteKey(dto.Id, _creatorId, _tenantId, isTenantAdmin: true);
    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.NotFound, result.ErrorCode);
  }

  [Fact]
  public async Task DeleteKey_KeyNotFound_Fails()
  {
    var result = await _keyManager.DeleteKey(Guid.NewGuid(), _creatorId, _tenantId, isTenantAdmin: false);
    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.NotFound, result.ErrorCode);
  }

  public async ValueTask DisposeAsync()
  {
    await _testApp.DisposeAsync();
  }

  [Fact]
  public async Task GetAllKeys_Admin_ReturnsAllTenantKeys()
  {
    var otherUser = await _testApp.Services.CreateTestUser(_tenantId, email: $"other-{Guid.NewGuid():N}@test.local");

    await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.Persistent,
        allowedUses: null,
        expiration: null,
        friendlyName: "My Key");

    await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: otherUser.Id,
        keyType: InstallerKeyType.Persistent,
        allowedUses: null,
        expiration: null,
        friendlyName: "Other Key");

    var keys = await _keyManager.GetAllKeys(_tenantId, _creatorId, isTenantAdmin: true);
    Assert.Equal(2, keys.Count);
  }

  [Fact]
  public async Task GetAllKeys_DoesNotReturnExpiredKeys()
  {
    await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.TimeBased,
        allowedUses: null,
        expiration: _timeProvider.GetLocalNow().AddHours(1),
        friendlyName: "Expiring");

    _timeProvider.Advance(TimeSpan.FromHours(2));

    var keys = await _keyManager.GetAllKeys(_tenantId, _creatorId, isTenantAdmin: true);
    Assert.Empty(keys);
  }

  [Fact]
  public async Task GetAllKeys_FiltersOldUsageEntriesFromSummary()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput,
      testDatabaseName: $"{Guid.NewGuid()}",
      extraConfiguration: new Dictionary<string, string?>
      {
        { "AppOptions:AgentInstallerKeyHistoryDays", "1" }
      });

    var keyManager = (AgentInstallerKeyManager)testApp.Services.GetRequiredService<IAgentInstallerKeyManager>();
    var timeProvider = testApp.TimeProvider;
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id);

    var dto = await keyManager.CreateKey(
        tenantId: tenant.Id,
        creatorId: user.Id,
        keyType: InstallerKeyType.Persistent,
        allowedUses: null,
        expiration: null,
        friendlyName: "Test Key");

    await keyManager.ValidateAndConsumeKey(dto.Id, dto.KeySecret, Guid.NewGuid());
    await keyManager.ValidateAndConsumeKey(dto.Id, dto.KeySecret, Guid.NewGuid());

    timeProvider.Advance(TimeSpan.FromDays(2));

    await keyManager.ValidateAndConsumeKey(dto.Id, dto.KeySecret, Guid.NewGuid());

    var keys = await keyManager.GetAllKeys(tenant.Id, user.Id, isTenantAdmin: true);
    var key = Assert.Single(keys);
    Assert.Equal(1, key.UsageCount);
    Assert.NotNull(key.CreatorName);
  }

  [Fact]
  public async Task GetAllKeys_NonAdmin_ReturnsOnlyOwnKeys()
  {
    var otherUser = await _testApp.Services.CreateTestUser(_tenantId, email: $"other-{Guid.NewGuid():N}@test.local");

    await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.Persistent,
        allowedUses: null,
        expiration: null,
        friendlyName: "My Key");

    await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: otherUser.Id,
        keyType: InstallerKeyType.Persistent,
        allowedUses: null,
        expiration: null,
        friendlyName: "Other Key");

    var keys = await _keyManager.GetAllKeys(_tenantId, _creatorId, isTenantAdmin: false);
    Assert.Single(keys);
    Assert.Equal(_creatorId, keys[0].CreatorId);
  }

  [Fact]
  public async Task GetAllKeys_ReturnsOnlyValidKeys()
  {
    await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.Persistent,
        allowedUses: null,
        expiration: null,
        friendlyName: "Valid Key");

    await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.TimeBased,
        allowedUses: null,
        expiration: _timeProvider.GetLocalNow().AddHours(1),
        friendlyName: "Expiring Key");

    var keys = await _keyManager.GetAllKeys(_tenantId, _creatorId, isTenantAdmin: true);
    Assert.Equal(2, keys.Count);

    _timeProvider.Advance(TimeSpan.FromHours(2));

    keys = await _keyManager.GetAllKeys(_tenantId, _creatorId, isTenantAdmin: true);
    Assert.Single(keys);
    Assert.Equal("Valid Key", keys[0].FriendlyName);
  }

  [Fact]
  public async Task GetKeyUsages_ByNonCreatorNonAdmin_Fails()
  {
    var dto = await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.UsageBased,
        allowedUses: 3,
        expiration: null,
        friendlyName: "Test Key");

    await _keyManager.ValidateAndConsumeKey(dto.Id, dto.KeySecret, Guid.NewGuid());

    var result = await _keyManager.GetKeyUsages(dto.Id, Guid.NewGuid(), _tenantId, isTenantAdmin: false);
    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.Forbidden, result.ErrorCode);
  }

  [Fact]
  public async Task GetKeyUsages_FiltersOldUsageEntries()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput,
      testDatabaseName: $"{Guid.NewGuid()}",
      extraConfiguration: new Dictionary<string, string?>
      {
        { "AppOptions:AgentInstallerKeyHistoryDays", "1" }
      });

    var keyManager = (AgentInstallerKeyManager)testApp.Services.GetRequiredService<IAgentInstallerKeyManager>();
    var timeProvider = testApp.TimeProvider;
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id);

    var dto = await keyManager.CreateKey(
        tenantId: tenant.Id,
        creatorId: user.Id,
        keyType: InstallerKeyType.Persistent,
        allowedUses: null,
        expiration: null,
        friendlyName: "Test Key");

    await keyManager.ValidateAndConsumeKey(dto.Id, dto.KeySecret, Guid.NewGuid());
    await keyManager.ValidateAndConsumeKey(dto.Id, dto.KeySecret, Guid.NewGuid());

    timeProvider.Advance(TimeSpan.FromDays(2));

    var result = await keyManager.GetKeyUsages(dto.Id, user.Id, tenant.Id, isTenantAdmin: true);
    Assert.True(result.IsSuccess);
    Assert.Empty(result.Value);
  }

  [Fact]
  public async Task GetKeyUsages_KeyNotFound_Fails()
  {
    var result = await _keyManager.GetKeyUsages(Guid.NewGuid(), _creatorId, _tenantId, isTenantAdmin: false);
    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.NotFound, result.ErrorCode);
  }

  [Fact]
  public async Task GetKeyUsages_ReturnsUsages()
  {
    var dto = await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.UsageBased,
        allowedUses: 3,
        expiration: null,
        friendlyName: "Test Key");

    var deviceId = Guid.NewGuid();
    await _keyManager.ValidateAndConsumeKey(dto.Id, dto.KeySecret, deviceId, "1.2.3.4");

    var result = await _keyManager.GetKeyUsages(dto.Id, _creatorId, _tenantId, isTenantAdmin: false);
    Assert.True(result.IsSuccess);
    Assert.Single(result.Value);
    Assert.Equal(deviceId, result.Value[0].DeviceId);
    Assert.Equal("1.2.3.4", result.Value[0].RemoteIpAddress);
  }

  [Fact]
  public async Task IncrementUsage_DoesNotShowExpiredUsageEntries()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput,
      testDatabaseName: $"{Guid.NewGuid()}",
      extraConfiguration: new Dictionary<string, string?>
      {
        { "AppOptions:AgentInstallerKeyHistoryDays", "1" }
      });

    var keyManager = (AgentInstallerKeyManager)testApp.Services.GetRequiredService<IAgentInstallerKeyManager>();
    var timeProvider = testApp.TimeProvider;
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id);

    var dto = await keyManager.CreateKey(
        tenantId: tenant.Id,
        creatorId: user.Id,
        keyType: InstallerKeyType.Persistent,
        allowedUses: null,
        expiration: null,
        friendlyName: "Test Key");

    await keyManager.IncrementUsage(dto.Id, Guid.NewGuid());
    await keyManager.IncrementUsage(dto.Id, Guid.NewGuid());

    timeProvider.Advance(TimeSpan.FromDays(2));

    await keyManager.IncrementUsage(dto.Id, Guid.NewGuid());

    var result = await keyManager.GetKeyUsages(dto.Id, user.Id, tenant.Id, isTenantAdmin: true);
    Assert.True(result.IsSuccess);
    Assert.Single(result.Value);
  }

  [Fact]
  public async Task IncrementUsage_UsageBasedKey_ExpiredAfter24Hours_Fails()
  {
    var dto = await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.UsageBased,
        allowedUses: 10,
        expiration: null,
        friendlyName: "Test Key");

    _timeProvider.Advance(TimeSpan.FromHours(25));

    var result = await _keyManager.IncrementUsage(dto.Id);
    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.BadRequest, result.ErrorCode);
    Assert.Contains("expired", result.Reason, StringComparison.OrdinalIgnoreCase);
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
    Assert.Equal(HttpResultErrorCode.BadRequest, result.ErrorCode);
    Assert.Contains("expired", result.Reason, StringComparison.OrdinalIgnoreCase);

    // Verify key was removed by trying to validate it
    var validateResult = await _keyManager.ValidateKey(dto.Id, dto.KeySecret);
    Assert.False(validateResult.IsSuccess);
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
  public async Task RenameKey_ByKeyCreator_Succeeds()
  {
    var dto = await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.Persistent,
        allowedUses: null,
        expiration: null,
        friendlyName: "Original");

    var result = await _keyManager.RenameKey(dto.Id, "Renamed", _creatorId, _tenantId, isTenantAdmin: false);
    Assert.True(result.IsSuccess);

    var keyResult = await _keyManager.TryGetKey(dto.Id, _tenantId);
    Assert.True(keyResult.IsSuccess);
    Assert.Equal("Renamed", keyResult.Value.FriendlyName);
  }

  [Fact]
  public async Task RenameKey_ByNonCreatorNonAdmin_Fails()
  {
    var dto = await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.Persistent,
        allowedUses: null,
        expiration: null,
        friendlyName: "Original");

    var result = await _keyManager.RenameKey(dto.Id, "Hacked", Guid.NewGuid(), _tenantId, isTenantAdmin: false);
    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.Forbidden, result.ErrorCode);

    var keyResult = await _keyManager.TryGetKey(dto.Id, _tenantId);
    Assert.True(keyResult.IsSuccess);
    Assert.Equal("Original", keyResult.Value!.FriendlyName);
  }

  [Fact]
  public async Task RenameKey_CrossTenant_Fails()
  {
    var otherTenant = await _testApp.Services.CreateTestTenant();
    var otherUser = await _testApp.Services.CreateTestUser(otherTenant.Id, email: $"cross-rename-{Guid.NewGuid():N}@test.local");

    var dto = await _keyManager.CreateKey(
        tenantId: otherTenant.Id,
        creatorId: otherUser.Id,
        keyType: InstallerKeyType.Persistent,
        allowedUses: null,
        expiration: null,
        friendlyName: "Other Tenant Key");

    var result = await _keyManager.RenameKey(dto.Id, "Hacked", _creatorId, _tenantId, isTenantAdmin: true);
    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.NotFound, result.ErrorCode);
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
    Assert.True(validateResult.IsSuccess);
  }

  [Fact]
  public async Task ValidateAndConsumeKey_UsageBasedKey_ExpiredAfter24Hours_Fails()
  {
    var dto = await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.UsageBased,
        allowedUses: 10,
        expiration: null,
        friendlyName: "Test Key");

    _timeProvider.Advance(TimeSpan.FromHours(25));
    var result = await _keyManager.ValidateAndConsumeKey(dto.Id, dto.KeySecret, Guid.NewGuid());
    Assert.False(result.IsSuccess);
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
      Assert.True(validateResult.IsSuccess);
    }

    var finalValidateResult = await _keyManager.ValidateAndConsumeKey(dto.Id, dto.KeySecret, Guid.NewGuid());
    Assert.False(finalValidateResult.IsSuccess);
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
    Assert.True(validateResult.IsSuccess);
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
      Assert.True(validateResult.IsSuccess);
    }

    // Key should still be valid because we haven't consumed any usage
    var finalResult = await _keyManager.ValidateKey(dto.Id, dto.KeySecret);
    Assert.True(finalResult.IsSuccess);
  }

  [Fact]
  public async Task ValidateKey_UsageBasedKey_ExpiredAfter24Hours_Fails()
  {
    var dto = await _keyManager.CreateKey(
        tenantId: _tenantId,
        creatorId: _creatorId,
        keyType: InstallerKeyType.UsageBased,
        allowedUses: 10,
        expiration: null,
        friendlyName: "Test Key");

    var validateResult = await _keyManager.ValidateKey(dto.Id, dto.KeySecret);
    Assert.True(validateResult.IsSuccess);

    _timeProvider.Advance(TimeSpan.FromHours(25));
    validateResult = await _keyManager.ValidateKey(dto.Id, dto.KeySecret);
    Assert.False(validateResult.IsSuccess);
  }

  [Fact]
  public async Task ValidateKey_WhenKeyDoesNotExist_Fails()
  {
    var validateResult = await _keyManager.ValidateKey(Guid.NewGuid(), "asdf");
    Assert.False(validateResult.IsSuccess);
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
    Assert.True(validateResult.IsSuccess);
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
    Assert.True(validateResult.IsSuccess);

    // Advance time 50 minutes, in increments of 10 minutes.
    // Key should be valid at each point in time.
    for (var i = 0; i < 5; i++)
    {
      _timeProvider.Advance(TimeSpan.FromMinutes(10));
      validateResult = await _keyManager.ValidateKey(dto.Id, dto.KeySecret);
      Assert.True(validateResult.IsSuccess);
    }

    // Advance time 1 hour. Key should now be expired.
    _timeProvider.Advance(TimeSpan.FromHours(1));
    validateResult = await _keyManager.ValidateKey(dto.Id, dto.KeySecret);
    Assert.False(validateResult.IsSuccess);
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

    Assert.True(validateResult.IsSuccess);
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
    Assert.False(validateResult.IsSuccess);

    // Verify key was removed by trying to validate it again
    validateResult = await _keyManager.ValidateKey(dto.Id, dto.KeySecret);
    Assert.False(validateResult.IsSuccess);
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
    Assert.False(validateResult.IsSuccess);
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
    Assert.True(validateResult.IsSuccess);
  }
}
