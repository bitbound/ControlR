using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ControlR.Web.Server.Tests;

public class CredentialManagementApiTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task RequirePasswordChange_BlocksPatAccess_UntilPasswordIsChanged()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutputHelper);

    var services = testServer.Services;
    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, "api-user@t.local");
    var patManager = services.GetRequiredService<IPersonalAccessTokenManager>();
    var passwordManager = services.GetRequiredService<IPasswordManager>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    var patResult = await patManager.CreateToken(new CreatePersonalAccessTokenRequestDto("Credential Test PAT"), user.Id);
    var personalAccessToken = patResult.Value!.PlainTextToken;

    var resetResult = await passwordManager.AdminResetPassword(tenant.Id, user.Id);
    Assert.True(resetResult.IsSuccess);
    var temporaryPassword = resetResult.Value!.TemporaryPassword;

    using var client = testServer.Factory.CreateClient();
    client.DefaultRequestHeaders.Add(PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName, personalAccessToken);

    var blockedResponse = await client.GetAsync("/api/personal-access-tokens", TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.Forbidden, blockedResponse.StatusCode);

    var changePasswordResponse = await client.PostAsJsonAsync(
      "/api/auth/change-password",
      new ChangePasswordRequestDto(temporaryPassword, "B3tt3rP@ssw0rd!"),
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, changePasswordResponse.StatusCode);

    var allowedResponse = await client.GetAsync("/api/personal-access-tokens", TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);

    using var verificationScope = services.CreateScope();
    var verificationUserManager = verificationScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var refreshedUser = await verificationUserManager.FindByIdAsync(user.Id.ToString());
    Assert.NotNull(refreshedUser);
    Assert.False(refreshedUser.RequirePasswordChange);
    Assert.True(await verificationUserManager.CheckPasswordAsync(refreshedUser, "B3tt3rP@ssw0rd!"));
  }
}