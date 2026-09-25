using System.Net;
using System.Net.Http.Json;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class InvitesControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task GetAll_ReadOnlyUser_DoesNotExposeActivationCode()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: new Dictionary<string, string?>
      {
        ["AppOptions:PublicBaseUrl"] = "https://test.example.com",
      });
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    await services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");

    var invitesProvider = services.GetRequiredService<ITenantInvitesProvider>();
    var createResult = await invitesProvider.CreateInvite(
      "invitee@t.local", tenant.Id, TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess);
    var activationCode = createResult.Value.InviteUrl.Segments[^1];

    var readOnlyUser = await services.CreateTestUser(tenant.Id, $"reader-{Guid.NewGuid():N}@t.local");
    await SeedAssignment(testServer, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      readOnlyUser.Id,
      PermissionNames.TenantUsersRead,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, readOnlyUser.Id, tenant.Id, "test")));

    using var readerClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, readOnlyUser.Id, readOnlyUser.TenantId, "test"));
    var readerResponse = await readerClient.GetAsync(
      HttpConstants.Internal.InvitesEndpoint, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, readerResponse.StatusCode);

    var readerInvites = await readerResponse.Content.ReadFromJsonAsync<InternalDtos.InviteResponseDto[]>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(readerInvites);
    Assert.NotEmpty(readerInvites);
    Assert.DoesNotContain(readerInvites, x => x.InviteUrl.ToString().Contains(activationCode));

    var writeUser = await services.CreateTestUser(
      tenant.Id, $"writer-{Guid.NewGuid():N}@t.local", PermissionPresets.TenantAdministrator);

    using var writerClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, writeUser.Id, writeUser.TenantId, "test"));
    var writerResponse = await writerClient.GetAsync(
      HttpConstants.Internal.InvitesEndpoint, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, writerResponse.StatusCode);

    var writerInvites = await writerResponse.Content.ReadFromJsonAsync<InternalDtos.InviteResponseDto[]>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(writerInvites);
    Assert.Contains(writerInvites, x => x.InviteUrl.ToString().Contains(activationCode));
  }

  private static async Task<HttpClient> CreatePatClient(TestWebServer testServer, PrincipalDescriptor actor)
  {
    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Invites Test PAT", PersonalAccessTokenPermissionMode.InheritOwner), actor.PrincipalId, actor);
    Assert.True(patResult.IsSuccess);

    var client = testServer.Factory.CreateClient();
    client.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);
    return client;
  }

  private static async Task SeedAssignment(TestWebServer testServer, PermissionAssignment assignment)
  {
    using var scope = testServer.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<ControlR.Web.Server.Data.AppDb>();
    db.PermissionAssignments.Add(assignment);
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }
}
