using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Web.Server.Api;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
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
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var tenant = new Tenant { Name = "Test Tenant" };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync();

    // Create some API keys
    var request1 = new CreateApiKeyRequestDto("Key 1");
    var request2 = new CreateApiKeyRequestDto("Key 2");
    await apiKeyManager.CreateWithKey(request1, tenant.Id);
    await apiKeyManager.CreateWithKey(request2, tenant.Id);

    var controller = CreateController(testApp, tenant.Id, userManager);

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
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var tenant = new Tenant { Name = "Test Tenant" };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync();

    var controller = CreateController(testApp, tenant.Id, userManager);
    var request = new CreateApiKeyRequestDto("New API Key");

    // Act
    var result = await controller.CreateApiKey(request);

    // Assert
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var response = Assert.IsType<CreateApiKeyResponseDto>(okResult.Value);
    Assert.Equal("New API Key", response.ApiKey.FriendlyName);
    Assert.NotNull(response.PlainTextKey);
    Assert.Equal(64, response.PlainTextKey.Length);
  }

  [Fact]
  public async Task UpdateApiKey_ShouldUpdateApiKey_AndReturnUpdatedKey()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var tenant = new Tenant { Name = "Test Tenant" };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync();

    // Create an API key first
    var createRequest = new CreateApiKeyRequestDto("Original Name");
    var createResult = await apiKeyManager.CreateWithKey(createRequest, tenant.Id);

    var controller = CreateController(testApp, tenant.Id, userManager);
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
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var tenant = new Tenant { Name = "Test Tenant" };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync();

    var controller = CreateController(testApp, tenant.Id, userManager);
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
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var tenant = new Tenant { Name = "Test Tenant" };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync();

    // Create an API key first
    var createRequest = new CreateApiKeyRequestDto("To Be Deleted");
    var createResult = await apiKeyManager.CreateWithKey(createRequest, tenant.Id);

    var controller = CreateController(testApp, tenant.Id, userManager);

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
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var tenant = new Tenant { Name = "Test Tenant" };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync();

    var controller = CreateController(testApp, tenant.Id, userManager);

    // Act
    var result = await controller.DeleteApiKey(Guid.NewGuid());

    // Assert
    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task CreateApiKey_ShouldReturnBadRequest_ForInvalidModel()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var tenant = new Tenant { Name = "Test Tenant" };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync();

    var controller = CreateController(testApp, tenant.Id, userManager);
    
    // Add model state error to simulate validation failure
    controller.ModelState.AddModelError("FriendlyName", "Required");

    var request = new CreateApiKeyRequestDto("");

    // Act
    var result = await controller.CreateApiKey(request);

    // Assert
    Assert.IsType<BadRequestObjectResult>(result.Result);
  }

  [Fact]
  public async Task UpdateApiKey_ShouldReturnBadRequest_ForInvalidModel()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var tenant = new Tenant { Name = "Test Tenant" };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync();

    var controller = CreateController(testApp, tenant.Id, userManager);
    
    // Add model state error to simulate validation failure
    controller.ModelState.AddModelError("FriendlyName", "Required");

    var request = new UpdateApiKeyRequestDto("");

    // Act
    var result = await controller.UpdateApiKey(Guid.NewGuid(), request);

    // Assert
    Assert.IsType<BadRequestObjectResult>(result.Result);
  }

  private ApiKeysController CreateController(TestApp testApp, Guid tenantId, UserManager<AppUser> userManager)
  {
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();
    
    var controller = new ApiKeysController(apiKeyManager, userManager);
    
    // Create a mock user
    var userId = Guid.NewGuid();
    var user = new AppUser { Id = userId, TenantId = tenantId };
    
    // Set up the controller context with a mock user
    var claims = new List<Claim>
    {
      new("TenantId", tenantId.ToString()),
      new(ClaimTypes.NameIdentifier, userId.ToString())
    };
    
    var identity = new ClaimsIdentity(claims, "Test");
    var principal = new ClaimsPrincipal(identity);
    
    controller.ControllerContext = new ControllerContext
    {
      HttpContext = new DefaultHttpContext
      {
        User = principal,
        RequestServices = testApp.App.Services
      }
    };

    // Mock the GetUserAsync method by creating the user in the system
    using var db = testApp.App.Services.GetRequiredService<AppDb>();
    db.Users.Add(user);
    db.SaveChanges();
    
    return controller;
  }
}
