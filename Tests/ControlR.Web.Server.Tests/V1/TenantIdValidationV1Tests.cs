using System.Net;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.PermissionAssignments;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// The V1 empty-tenant-id contract. A tenantId that is absent or unparseable binds to Guid.Empty,
/// which is a malformed request and answers 400. A well-formed tenant the caller does not belong to
/// is an authorization failure and still answers 403.
/// </summary>
public class TenantIdValidationV1Tests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task Get_WithEmptyTenantId_ReturnsBadRequest()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    var user = await testServer.Services.CreateTestUser(
      tenant.Id, $"empty-tenant-{Guid.NewGuid():N}@t.local");
    using var httpClient = await CreatePatClient(testServer, user.Id, tenant.Id);

    var response = await httpClient.GetAsync(
      $"{HttpConstants.V1.TagsEndpoint}?tenantId={Guid.Empty}",
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Get_WithForeignTenantId_ReturnsForbidden()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    var foreignTenant = await testServer.Services.CreateTestTenant("Tenant Id Validation Foreign");
    var user = await testServer.Services.CreateTestUser(
      tenant.Id, $"foreign-tenant-{Guid.NewGuid():N}@t.local");
    using var httpClient = await CreatePatClient(testServer, user.Id, tenant.Id);

    var response = await httpClient.GetAsync(
      $"{HttpConstants.V1.TagsEndpoint}?tenantId={foreignTenant.Id}",
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  private async Task<HttpClient> CreatePatClient(TestWebServer testServer, Guid userId, Guid tenantId)
  {
    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var actor = new PrincipalDescriptor(PrincipalType.User, userId, tenantId, "test");
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto(
        "Tenant Id Validation PAT", PersonalAccessTokenPermissionMode.InheritOwner),
      userId,
      actor);
    Assert.True(patResult.IsSuccess);

    var client = testServer.Factory.CreateClient();
    client.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);
    return client;
  }
}
