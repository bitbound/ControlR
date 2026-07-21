using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;
using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests.V1;

public class TenantsControllerIntegrationTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task CreateEmptyNameTenant_ViaServiceAccount_ReturnsBadRequest()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var saManager = testServer.Services.GetRequiredService<IServiceAccountManager>();
    var saResult = await saManager.CreateForServer(
      "Integration Test SA - Bad Request",
      null,
      TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    httpClient.DefaultRequestHeaders.Add(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName,
      saResult.Value.PlainTextSecretKey);

    var response = await httpClient.PostAsJsonAsync(
      HttpConstants.V1.TenantsEndpoint,
      new CreateTenantRequestDto(""),
      TestContext.Current.CancellationToken);

    Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task CreateTenantAndRetrieve_ViaServiceAccount_CompletesFullFlow()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var saManager = testServer.Services.GetRequiredService<IServiceAccountManager>();
    var saResult = await saManager.CreateForServer(
      "Integration Test SA - Full Flow",
      null,
      TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    httpClient.DefaultRequestHeaders.Add(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName,
      saResult.Value.PlainTextSecretKey);

    var createName = "Full Flow Tenant";
    var createResponse = await httpClient.PostAsJsonAsync(
      HttpConstants.V1.TenantsEndpoint,
      new CreateTenantRequestDto(createName),
      TestContext.Current.CancellationToken);

    Assert.Equal(System.Net.HttpStatusCode.Created, createResponse.StatusCode);
    var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(created);

    var getResponse = await httpClient.GetAsync(
      $"{HttpConstants.V1.TenantsEndpoint}/{created.TenantId}",
      TestContext.Current.CancellationToken);

    Assert.Equal(System.Net.HttpStatusCode.OK, getResponse.StatusCode);
    var retrieved = await getResponse.Content.ReadFromJsonAsync<GetTenantResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(retrieved);
    Assert.Equal(created.TenantId, retrieved.TenantId);
    Assert.Equal(createName, retrieved.TenantName);
  }

  [Fact]
  public async Task CreateTenant_ViaNonAdminPatUser_ReturnsForbidden()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    using var httpClient = testServer.Factory.CreateClient();

    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(tenant.Id, email: "seed@t.local");
    var user = await testServer.Services.CreateTestUser(
      tenant.Id,
      "non-admin@test.local",
      RoleNames.DeviceSuperUser);

    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Test PAT"),
      user.Id);
    Assert.True(patResult.IsSuccess, $"PAT creation failed: {patResult.Reason}");

    httpClient.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);

    var response = await httpClient.PostAsJsonAsync(
      HttpConstants.V1.TenantsEndpoint,
      new CreateTenantRequestDto("Forbidden Tenant"),
      TestContext.Current.CancellationToken);

    Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task CreateTenant_ViaServerAdminPatUser_ReturnsCreatedAt()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    using var httpClient = testServer.Factory.CreateClient();

    var tenant = await testServer.Services.CreateTestTenant();
    var user = await testServer.Services.CreateTestUser(
      tenant.Id,
      "server-admin@test.local",
      RoleNames.ServerAdministrator);

    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Server Admin PAT"),
      user.Id);
    Assert.True(patResult.IsSuccess, $"PAT creation failed: {patResult.Reason}");

    httpClient.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);

    var response = await httpClient.PostAsJsonAsync(
      HttpConstants.V1.TenantsEndpoint,
      new CreateTenantRequestDto("Admin Tenant"),
      TestContext.Current.CancellationToken);

    Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
    var created = await response.Content.ReadFromJsonAsync<CreateTenantResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(created);
    Assert.Equal("Admin Tenant", created.TenantName);
    Assert.NotEqual(Guid.Empty, created.TenantId);
  }

  [Fact]
  public async Task CreateTenant_ViaServiceAccount_ReturnsCreatedAt()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var saManager = testServer.Services.GetRequiredService<IServiceAccountManager>();
    var saResult = await saManager.CreateForServer(
      "Integration Test SA - Tenant Create",
      null,
      TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    httpClient.DefaultRequestHeaders.Add(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName,
      saResult.Value.PlainTextSecretKey);

    var request = new CreateTenantRequestDto("Integration Test Tenant");
    var response = await httpClient.PostAsJsonAsync(
      HttpConstants.V1.TenantsEndpoint,
      request,
      TestContext.Current.CancellationToken);

    Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
    var result = await response.Content.ReadFromJsonAsync<CreateTenantResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(result);
    Assert.Equal("Integration Test Tenant", result.TenantName);
    Assert.NotEqual(Guid.Empty, result.TenantId);
  }

  [Fact]
  public async Task GetNonExistentTenant_ViaServiceAccount_ReturnsNotFound()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var saManager = testServer.Services.GetRequiredService<IServiceAccountManager>();
    var saResult = await saManager.CreateForServer(
      "Integration Test SA - Not Found",
      null,
      TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    httpClient.DefaultRequestHeaders.Add(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName,
      saResult.Value.PlainTextSecretKey);

    var response = await httpClient.GetAsync(
      $"{HttpConstants.V1.TenantsEndpoint}/{Guid.NewGuid()}",
      TestContext.Current.CancellationToken);

    Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task GetTenant_ViaServiceAccount_ReturnsOk()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var saManager = testServer.Services.GetRequiredService<IServiceAccountManager>();
    var saResult = await saManager.CreateForServer(
      "Integration Test SA - Tenant Get",
      null,
      TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    httpClient.DefaultRequestHeaders.Add(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName,
      saResult.Value.PlainTextSecretKey);

    var createResponse = await httpClient.PostAsJsonAsync(
      HttpConstants.V1.TenantsEndpoint,
      new CreateTenantRequestDto("Get Test Tenant"),
      TestContext.Current.CancellationToken);

    Assert.Equal(System.Net.HttpStatusCode.Created, createResponse.StatusCode);
    var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(created);

    var getResponse = await httpClient.GetAsync(
      $"{HttpConstants.V1.TenantsEndpoint}/{created.TenantId}",
      TestContext.Current.CancellationToken);

    Assert.Equal(System.Net.HttpStatusCode.OK, getResponse.StatusCode);
    var getResult = await getResponse.Content.ReadFromJsonAsync<GetTenantResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(getResult);
    Assert.Equal(created.TenantId, getResult.TenantId);
    Assert.Equal("Get Test Tenant", getResult.TenantName);
  }

  [Fact]
  public async Task UnauthenticatedTenantRequest_Returns401()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var response = await httpClient.PostAsJsonAsync(
      HttpConstants.V1.TenantsEndpoint,
      new CreateTenantRequestDto("Unauthorized Test Tenant"),
      TestContext.Current.CancellationToken);

    Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
  }
}