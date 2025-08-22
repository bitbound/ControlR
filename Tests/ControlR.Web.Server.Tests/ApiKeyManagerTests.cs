using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit.Abstractions;

namespace ControlR.Web.Server.Tests;

public class ApiKeyManagerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task CreateWithKey_ShouldCreateApiKey_AndReturnPlainTextKey()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var tenant = await testApp.CreateTestTenant();
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var request = new CreateApiKeyRequestDto("Test API Key");

    // Act
    var result = await apiKeyManager.CreateWithKey(request, tenant.Id);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
    Assert.Equal("Test API Key", result.Value.ApiKey.FriendlyName);
    Assert.NotNull(result.Value.PlainTextKey);
    Assert.Equal(64, result.Value.PlainTextKey.Length); // Should be 64 characters

    // Verify stored in database
    var storedKey = await db.ApiKeys.FirstOrDefaultAsync(x => x.Id == result.Value.ApiKey.Id);
    Assert.NotNull(storedKey);
    Assert.Equal("Test API Key", storedKey.FriendlyName);
    Assert.Equal(tenant.Id, storedKey.TenantId);
    Assert.NotEqual(result.Value.PlainTextKey, storedKey.HashedKey); // Should be hashed
  }

  [Fact]
  public async Task GetAll_ShouldReturnApiKeysForTenant()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var tenant = await testApp.CreateTestTenant();
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();

    // Create test data
    var request1 = new CreateApiKeyRequestDto("API Key 1");
    var request2 = new CreateApiKeyRequestDto("API Key 2");
    
    await apiKeyManager.CreateWithKey(request1, tenant.Id);
    await apiKeyManager.CreateWithKey(request2, tenant.Id);

    // Act
    var result = await apiKeyManager.GetAll();

    // Assert
    Assert.Equal(2, result.Count());
    Assert.Contains(result, x => x.FriendlyName == "API Key 1");
    Assert.Contains(result, x => x.FriendlyName == "API Key 2");
  }

  [Fact]
  public async Task Update_ShouldUpdateFriendlyName()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var tenant = await testApp.CreateTestTenant();
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var createRequest = new CreateApiKeyRequestDto("Original Name");
    var createResult = await apiKeyManager.CreateWithKey(createRequest, tenant.Id);

    var updateRequest = new UpdateApiKeyRequestDto("Updated Name");

    // Act
    var result = await apiKeyManager.Update(createResult.Value!.ApiKey.Id, updateRequest);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal("Updated Name", result.Value.FriendlyName);

    // Verify in database
    var storedKey = await db.ApiKeys.FirstOrDefaultAsync(x => x.Id == createResult.Value!.ApiKey.Id);
    Assert.NotNull(storedKey);
    Assert.Equal("Updated Name", storedKey.FriendlyName);
  }

  [Fact]
  public async Task Delete_ShouldRemoveApiKey()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var tenant = await testApp.CreateTestTenant();
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var createRequest = new CreateApiKeyRequestDto("To Be Deleted");
    var createResult = await apiKeyManager.CreateWithKey(createRequest, tenant.Id);

    // Act
    var result = await apiKeyManager.Delete(createResult.Value!.ApiKey.Id);

    // Assert
    Assert.True(result.IsSuccess);

    // Verify removed from database
    var storedKey = await db.ApiKeys.FirstOrDefaultAsync(x => x.Id == createResult.Value!.ApiKey.Id);
    Assert.Null(storedKey);
  }

  [Fact]
  public async Task ValidateApiKey_ShouldReturnTenantId_ForValidKey()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var tenant = await testApp.CreateTestTenant();
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();

    var createRequest = new CreateApiKeyRequestDto("Test Key");
    var createResult = await apiKeyManager.CreateWithKey(createRequest, tenant.Id);
    var plainTextKey = createResult.Value!.PlainTextKey;

    // Act
    var result = await apiKeyManager.ValidateApiKey(plainTextKey);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal(tenant.Id, result.Value);
  }

  [Fact]
  public async Task ValidateApiKey_ShouldReturnNull_ForInvalidKey()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();

    // Act
    var result = await apiKeyManager.ValidateApiKey("invalid-key");

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Null(result.Value);
  }

  [Fact]
  public async Task ValidateApiKey_ShouldUpdateLastUsedTimestamp()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var tenant = await testApp.CreateTestTenant();
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();
    var timeProvider = testApp.TimeProvider;
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var createRequest = new CreateApiKeyRequestDto("Test Key");
    var createResult = await apiKeyManager.CreateWithKey(createRequest, tenant.Id);
    var plainTextKey = createResult.Value!.PlainTextKey;

    // Advance time
    timeProvider.Advance(TimeSpan.FromHours(1));
    var expectedLastUsed = timeProvider.GetUtcNow();

    // Act
    await apiKeyManager.ValidateApiKey(plainTextKey);

    // Assert
    var storedKey = await db.ApiKeys
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(x => x.Id == createResult.Value!.ApiKey.Id);
    
    Assert.NotNull(storedKey);
    Assert.NotNull(storedKey.LastUsed);
    Assert.Equal(expectedLastUsed, storedKey.LastUsed);
  }

  [Fact]
  public async Task Update_ShouldReturnNotFound_ForNonExistentKey()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();

    var updateRequest = new UpdateApiKeyRequestDto("Updated Name");

    // Act
    var result = await apiKeyManager.Update(Guid.NewGuid(), updateRequest);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("not found", result.Reason);
  }

  [Fact]
  public async Task Delete_ShouldReturnNotFound_ForNonExistentKey()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();

    // Act
    var result = await apiKeyManager.Delete(Guid.NewGuid());

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("not found", result.Reason);
  }
}
