using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Web.Server.Api;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.Users;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ControlR.Web.Server.Tests;

public class InviteAcceptanceTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task AcceptInvite_ClearsOnlyUserRolesAndTags_RetainsTokensAndPreferences()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput, useInMemoryDatabase: false);

    AppUser adminUser;
    Guid tenantAId;

    // Step 1: Create adminUser in its own scope
    using (var scope = testApp.CreateScope())
    {
      var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();
      var adminResult = await userCreator.CreateUser("admin@example.com", "Password123!", returnUrl: null);
      Assert.True(adminResult.Succeeded);
      adminUser = adminResult.User;
      tenantAId = adminUser.TenantId;
    }

    Guid tenantBId;
    Guid user2Id;
    string activationCode;

    // Step 2: Create another tenant (Tenant B) with a temp user, then remove the user to have an empty tenant
    using (var scope = testApp.CreateScope())
    {
      var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();
      var tempUserResult = await userCreator.CreateUser("temp@example.com", "Password123!", returnUrl: null);
      Assert.True(tempUserResult.Succeeded);
      tenantBId = tempUserResult.User.TenantId;

      var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
      await userManager.DeleteAsync(tempUserResult.User);
    }

    // Step 3: Create tags for Tenant A
    using (var scope = testApp.CreateScope())
    {
      var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDb>>();
      await using var db = await dbFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);

      db.Tags.Add(new Tag
      {
        Id = Guid.NewGuid(),
        Name = "TenantA-Tag1",
        Type = TagType.Permission,
        TenantId = tenantAId
      });

      db.Tags.Add(new Tag
      {
        Id = Guid.NewGuid(),
        Name = "TenantA-Tag2",
        Type = TagType.Permission,
        TenantId = tenantAId
      });

      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Step 4: Create tags for Tenant B
    Guid tenantBTag1Id;
    Guid tenantBTag2Id;
    using (var scope = testApp.CreateScope())
    {
      var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDb>>();
      await using var db = await dbFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);

      tenantBTag1Id = Guid.NewGuid();
      tenantBTag2Id = Guid.NewGuid();

      db.Tags.Add(new Tag
      {
        Id = tenantBTag1Id,
        Name = "TenantB-Tag1",
        Type = TagType.Permission,
        TenantId = tenantBId
      });

      db.Tags.Add(new Tag
      {
        Id = tenantBTag2Id,
        Name = "TenantB-Tag2",
        Type = TagType.Permission,
        TenantId = tenantBId
      });

      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Step 5: AdminUser invites User2 to Tenant A (this creates user2 in Tenant A)
    using (var scope = testApp.CreateScope())
    {
      var controller = await scope.CreateControllerWithUser<InvitesController>(adminUser);
      controller.ControllerContext.HttpContext!.Request.Scheme = "https";
      controller.ControllerContext.HttpContext.Request.Host = new HostString("test.example.com");

      var tenantInvitesProvider = scope.ServiceProvider.GetRequiredService<ITenantInvitesProvider>();

      var result = await controller.Create(
        new TenantInviteRequestDto("user2@example.com"),
        tenantInvitesProvider);

      var okResult = Assert.IsType<OkObjectResult>(result.Result);
      var inviteResponse = Assert.IsType<TenantInviteResponseDto>(okResult.Value);
      activationCode = inviteResponse.InviteUrl.Segments[^1];
    }

    // Get user2's ID
    using (var scope = testApp.CreateScope())
    {
      var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDb>>();
      await using var db = await dbFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
      var user2 = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Email == "user2@example.com", cancellationToken: TestContext.Current.CancellationToken);
      user2Id = user2.Id;
    }

    // Step 6: Move user2 to Tenant B and assign tags, preferences, and tokens
    using (var scope = testApp.CreateScope())
    {
      var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDb>>();
      await using var db = await dbFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);

      // Move user2 to Tenant B
      var user2 = await db.Users
        .IgnoreQueryFilters()
        .Include(u => u.Tags)
        .FirstAsync(u => u.Id == user2Id, cancellationToken: TestContext.Current.CancellationToken);
      user2.TenantId = tenantBId;

      // Assign tags from Tenant B
      var tag1 = await db.Tags.IgnoreQueryFilters().FirstAsync(t => t.Id == tenantBTag1Id, cancellationToken: TestContext.Current.CancellationToken);
      var tag2 = await db.Tags.IgnoreQueryFilters().FirstAsync(t => t.Id == tenantBTag2Id, cancellationToken: TestContext.Current.CancellationToken);
      user2.Tags =
      [
        tag1,
        tag2
      ];

      // Create preferences in Tenant B
      db.UserPreferences.Add(new UserPreference
      {
        Id = Guid.NewGuid(),
        Name = "theme",
        Value = "dark",
        UserId = user2Id,
      });

      db.UserPreferences.Add(new UserPreference
      {
        Id = Guid.NewGuid(),
        Name = "language",
        Value = "en",
        UserId = user2Id,
      });

      // Create personal access tokens in Tenant B
      db.PersonalAccessTokens.Add(new PersonalAccessToken
      {
        Id = Guid.NewGuid(),
        Name = "Token1",
        HashedKey = "hashed-key-1",
        UserId = user2Id,
      });

      db.PersonalAccessTokens.Add(new PersonalAccessToken
      {
        Id = Guid.NewGuid(),
        Name = "Token2",
        HashedKey = "hashed-key-2",
        UserId = user2Id,
      });

      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Step 7: User2 accepts invite (moves back to Tenant A)
    using (var scope = testApp.CreateScope())
    {
      var controller = scope.CreateController<InvitesController>();

      var tenantInvitesProvider = scope.ServiceProvider.GetRequiredService<ITenantInvitesProvider>();

      var acceptResult = await controller.AcceptInvite(
        new AcceptInvitationRequestDto(activationCode, "user2@example.com", "NewPassword123!"),
        tenantInvitesProvider);

      var acceptResponse = Assert.IsType<AcceptInvitationResponseDto>(acceptResult.Value);
      Assert.True(acceptResponse.IsSuccessful);
    }

    // Step 8: Assert final state
    using (var scope = testApp.CreateScope())
    {
      var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDb>>();
      await using var db = await dbFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);

      // User2 should be in Tenant A
      var user2Final = await db.Users
        .IgnoreQueryFilters()
        .Include(u => u.Tags)
        .Include(u => u.UserPreferences)
        .Include(u => u.PersonalAccessTokens)
        .Include(u => u.UserRoles)
        .FirstAsync(u => u.Id == user2Id, cancellationToken: TestContext.Current.CancellationToken);

      Assert.Equal(tenantAId, user2Final.TenantId);
      
      // Only UserRoles and Tags should be cleared
      Assert.Empty(user2Final.UserRoles ?? []);
      Assert.Empty(user2Final.Tags ?? []);
      
      // PersonalAccessTokens and UserPreferences should be retained
      Assert.Equal(2, user2Final.PersonalAccessTokens?.Count ?? 0);
      Assert.Equal(2, user2Final.UserPreferences?.Count ?? 0);
    }
  }
}
