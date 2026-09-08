using System.Net;
using System.Net.Http.Json;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class TagsControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task GetAllTags_IncludeLinkedIds_OnlyExposesReadableDevices()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    await services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");

    var readableDevice = await services.CreateTestDevice(tenant.Id);
    var hiddenDevice = await services.CreateTestDevice(tenant.Id);

    var tagId = Guid.NewGuid();
    using (var scope = services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      var readable = await db.Devices.FindAsync([readableDevice.Id], TestContext.Current.CancellationToken);
      var hidden = await db.Devices.FindAsync([hiddenDevice.Id], TestContext.Current.CancellationToken);

      db.Tags.Add(new Tag
      {
        Id = tagId,
        Name = "Shared Tag",
        TenantId = tenant.Id,
        Devices = [readable!, hidden!],
      });
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    var user = await services.CreateTestUser(tenant.Id, $"scoped-{Guid.NewGuid():N}@t.local");
    await SeedAssignment(testServer, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      user.Id,
      PermissionNames.DeviceRead,
      PermissionScopeKind.Device,
      readableDevice.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, tenant.Id, "tags-test")));

    using var httpClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));

    var response = await httpClient.GetAsync(
      $"{HttpConstants.Internal.TagsEndpoint}?includeLinkedIds=true",
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var tags = await response.Content.ReadFromJsonAsync<InternalDtos.TagResponseDto[]>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(tags);

    var tag = Assert.Single(tags);
    Assert.Equal(tagId, tag.Id);
    Assert.Contains(readableDevice.Id, tag.DeviceIds);
    Assert.DoesNotContain(hiddenDevice.Id, tag.DeviceIds);
  }

  private static async Task<HttpClient> CreatePatClient(TestWebServer testServer, PrincipalDescriptor actor)
  {
    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Tags Test PAT", PersonalAccessTokenPermissionMode.InheritOwner), actor.PrincipalId, actor);
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
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.PermissionAssignments.Add(assignment);
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }
}
