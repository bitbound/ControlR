using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Settings;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.TenantSettings;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserPreferences;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// The settings upserts over HTTP, including whether the returned Location resolves.
/// </summary>
public class SettingsCreateStatusV1Tests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task SetTenantSetting_ViaHttp_Answers201WithAFollowableLocation()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    var user = await testServer.Services.CreateTestUser(
      tenant.Id,
      $"setting-create-{Guid.NewGuid():N}@t.local",
      PermissionPresets.TenantAdministrator);
    using var client = await CreateAuthenticatedClientAsync(testServer, user);

    using var response = await client.PostAsJsonAsync(
      $"{HttpConstants.V1.TenantSettingsEndpoint}?tenantId={tenant.Id}",
      new TenantSettingRequestDto(
        TenantSettingNames.NotifyUserOnSessionStart,
        TenantSettingDefinitions.FormatValue(
          TenantSettingNames.NotifyUserOnSessionStart,
          true) ?? "true"),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    Assert.NotNull(response.Headers.Location);

    var created = await response.Content.ReadFromJsonAsync<TenantSettingResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(created?.Id);

    using var fetched = await client.GetAsync(
      response.Headers.Location!,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);

    var atLocation = await fetched.Content.ReadFromJsonAsync<TenantSettingResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.Equal(created?.Id, atLocation?.Id);
    Assert.Equal(TenantSettingNames.NotifyUserOnSessionStart, atLocation?.Name);
  }

  [Fact]
  public async Task SetUserPreference_ViaHttp_Answers201WithAFollowableLocation()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    var user = await testServer.Services.CreateTestUser(
      tenant.Id,
      $"pref-create-{Guid.NewGuid():N}@t.local",
      PermissionPresets.TenantAdministrator);
    using var client = await CreateAuthenticatedClientAsync(testServer, user);

    using var response = await client.PostAsJsonAsync(
      $"{HttpConstants.V1.UserPreferencesEndpoint}?tenantId={tenant.Id}",
      new UserPreferenceRequestDto(UserPreferenceNames.ThemeMode, "dark"),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    Assert.NotNull(response.Headers.Location);

    testOutput.WriteLine($"Location: {response.Headers.Location}");

    var created = await response.Content.ReadFromJsonAsync<UserPreferenceResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(created?.Id);

    using var fetched = await client.GetAsync(
      response.Headers.Location!,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);

    var atLocation = await fetched.Content.ReadFromJsonAsync<UserPreferenceResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.Equal(created?.Id, atLocation?.Id);
    Assert.Equal(UserPreferenceNames.ThemeMode, atLocation?.Name);
  }

  private static async Task<HttpClient> CreateAuthenticatedClientAsync(
    TestWebServer testServer,
    AppUser user)
  {
    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto(
        "Settings create PAT",
        PersonalAccessTokenPermissionMode.InheritOwner),
      user.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));
    Assert.True(patResult.IsSuccess, patResult.Reason);

    var client = testServer.Factory.CreateClient();
    client.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);
    return client;
  }
}
