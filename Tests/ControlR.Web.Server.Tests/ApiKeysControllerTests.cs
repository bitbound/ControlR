using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Web.Server.Api;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using ControlR.Web.Client.Authz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace ControlR.Web.Server.Tests;

public class ApiKeysControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task GetApiKeys_ShouldReturnApiKeys_ForCurrentTenant()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var (controller, tenant, user) = await testApp.CreateControllerWithTestData<ApiKeysController>(
      roles: RoleNames.TenantAdministrator);
    
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();

    // Create some API keys
    var request1 = new CreateApiKeyRequestDto("Key 1");
    var request2 = new CreateApiKeyRequestDto("Key 2");
    await apiKeyManager.CreateKey(request1, tenant.Id);
    await apiKeyManager.CreateKey(request2, tenant.Id);

    // Act
    var result = await controller.GetApiKeys();

    // Assert
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var apiKeys = Assert.IsAssignableFrom<IEnumerable<ApiKeyDto>>(okResult.Value);
    Assert.Equal(2, apiKeys.Count());
    Assert.Contains(apiKeys, k => k.FriendlyName == "Key 1");
    Assert.Contains(apiKeys, k => k.FriendlyName == "Key 2");
  }

  [Fact]
  public async Task CreateApiKey_ShouldCreateApiKey_AndReturnResult()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var (controller, tenant, user) = await testApp.CreateControllerWithTestData<ApiKeysController>(
      roles: RoleNames.TenantAdministrator);

    var request = new CreateApiKeyRequestDto("New API Key");

    // Act
    var result = await controller.CreateApiKey(request);

    // Assert
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var response = Assert.IsType<CreateApiKeyResponseDto>(okResult.Value);
    Assert.Equal("New API Key", response.ApiKey.FriendlyName);
    Assert.NotNull(response.PlainTextKey);
    // 32 (GUID hex string) + 1 (:) + 64 (api key)
    Assert.Equal(97, response.PlainTextKey.Length); // Should be 97 characters
  }

  [Fact]
  public async Task UpdateApiKey_ShouldUpdateApiKey_AndReturnUpdatedKey()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var (controller, tenant, user) = await testApp.CreateControllerWithTestData<ApiKeysController>(
      roles: RoleNames.TenantAdministrator);
    
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();

    // Create an API key first
    var createRequest = new CreateApiKeyRequestDto("Original Name");
    var createResult = await apiKeyManager.CreateKey(createRequest, tenant.Id);

    var updateRequest = new UpdateApiKeyRequestDto("Updated Name");

    // Act
    var result = await controller.UpdateApiKey(createResult.Value!.ApiKey.Id, updateRequest);

    // Assert
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var apiKey = Assert.IsType<ApiKeyDto>(okResult.Value);
    Assert.Equal("Updated Name", apiKey.FriendlyName);
    Assert.Equal(createResult.Value!.ApiKey.Id, apiKey.Id);
  }

  [Fact]
  public async Task UpdateApiKey_ShouldReturnNotFound_ForNonExistentKey()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var (controller, tenant, user) = await testApp.CreateControllerWithTestData<ApiKeysController>(
      roles: RoleNames.TenantAdministrator);

    var updateRequest = new UpdateApiKeyRequestDto("Updated Name");

    // Act
    var result = await controller.UpdateApiKey(Guid.NewGuid(), updateRequest);

    // Assert
    Assert.IsType<BadRequestObjectResult>(result.Result);
  }

  [Fact]
  public async Task DeleteApiKey_ShouldDeleteApiKey_AndReturnOk()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var (controller, tenant, user) = await testApp.CreateControllerWithTestData<ApiKeysController>(
      roles: RoleNames.TenantAdministrator);
    
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();

    // Create an API key first
    var createRequest = new CreateApiKeyRequestDto("To Be Deleted");
    var createResult = await apiKeyManager.CreateKey(createRequest, tenant.Id);

    // Act
    var result = await controller.DeleteApiKey(createResult.Value!.ApiKey.Id);

    // Assert
    Assert.IsType<OkResult>(result);

    // Verify it's actually deleted
    var apiKeys = await apiKeyManager.GetAll();
    Assert.DoesNotContain(apiKeys, k => k.Id == createResult.Value!.ApiKey.Id);
  }

  [Fact]
  public async Task DeleteApiKey_ShouldReturnNotFound_ForNonExistentKey()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var (controller, tenant, user) = await testApp.CreateControllerWithTestData<ApiKeysController>(
      roles: RoleNames.TenantAdministrator);

    // Act
    var result = await controller.DeleteApiKey(Guid.NewGuid());

    // Assert
    Assert.IsType<BadRequestObjectResult>(result);
  }
}
