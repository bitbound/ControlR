using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class AppUserClaimsPrincipalFactoryTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task GenerateClaimsAsync_EmitsCanonicalPrincipalClaims()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, $"factory-{Guid.NewGuid():N}@t.local");

    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var factory = services.GetRequiredService<IUserClaimsPrincipalFactory<AppUser>>();

    var principal = await factory.CreateAsync(user);

    var typeClaim = principal.FindFirst(PrincipalClaimTypes.PrincipalType);
    Assert.NotNull(typeClaim);
    Assert.Equal(PrincipalClaimValues.User, typeClaim.Value);

    var idClaim = principal.FindFirst(PrincipalClaimTypes.PrincipalId);
    Assert.NotNull(idClaim);
    Assert.Equal(user.Id.ToString(), idClaim.Value);
  }
}
