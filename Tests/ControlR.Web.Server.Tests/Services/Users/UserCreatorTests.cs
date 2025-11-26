using System.Security.Claims;
using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Options;
using ControlR.Web.Server.Services.Users;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace ControlR.Web.Server.Tests.Services.Users;

public class UserCreatorTests(ITestOutputHelper output)
{
    [Fact]
    public async Task CreateUser_DuplicateEmail_Fails()
    {
        await using var testApp = await TestAppBuilder.CreateTestApp(output);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();

        await userCreator.CreateUser("duplicate@example.com", "Password123!", null);
        var result = await userCreator.CreateUser("duplicate@example.com", "Password123!", null);

        Assert.False(result.Succeeded);
        Assert.Contains(result.IdentityResult.Errors, e => e.Code == "DuplicateUserName");
    }

    [Fact]
    public async Task CreateUser_EmailSendingDisabled_ConfirmationNotRequired_SucceedsAndConfirmsEmail()
    {
        var config = new Dictionary<string, string?>
        {
            ["AppOptions:DisableEmailSending"] = "true",
            ["AppOptions:RequireUserEmailConfirmation"] = "false"
        };

        await using var testApp = await TestAppBuilder.CreateTestApp(output, extraConfiguration: config);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();

        var result = await userCreator.CreateUser("noconfirm@example.com", "Password123!", null);

        Assert.True(result.Succeeded);
        Assert.True(result.User!.EmailConfirmed);
    }

    [Fact]
    public async Task CreateUser_EmailSendingDisabled_ConfirmationRequired_Throws()
    {
        var config = new Dictionary<string, string?>
        {
            ["AppOptions:DisableEmailSending"] = "true",
            ["AppOptions:RequireUserEmailConfirmation"] = "true"
        };

        await using var testApp = await TestAppBuilder.CreateTestApp(output, extraConfiguration: config);
        using var scope = testApp.CreateScope();
        
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<AppOptions>>();
        output.WriteLine($"DisableEmailSending: {options.CurrentValue.DisableEmailSending}");
        output.WriteLine($"RequireUserEmailConfirmation: {options.CurrentValue.RequireUserEmailConfirmation}");

        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();

        var result = await userCreator.CreateUser("throw@example.com", "Password123!", null);
            
        Assert.False(result.Succeeded);
        Assert.Contains(result.IdentityResult.Errors, e => e.Description.Contains("Email sending is disabled"));
    }

    [Fact]
    public async Task CreateUser_ExistingTenant_DoesNotAssignDefaultRoles()
    {
        await using var testApp = await TestAppBuilder.CreateTestApp(output);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

        // Create first user so next one isn't server admin
        await userCreator.CreateUser("admin@example.com", "Password123!", null);

        var tenant = new Tenant { Name = "Existing Tenant" };
        appDb.Tenants.Add(tenant);
        await appDb.SaveChangesAsync();

        var result = await userCreator.CreateUser("user@example.com", "Password123!", tenant.Id);

        Assert.True(result.Succeeded);
        var roles = await userManager.GetRolesAsync(result.User!);
        Assert.Empty(roles);
    }

    [Fact]
    public async Task CreateUser_FirstUser_IsServerAdmin()
    {
        await using var testApp = await TestAppBuilder.CreateTestApp(output);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var result = await userCreator.CreateUser("admin@example.com", "Password123!", null);

        Assert.True(result.Succeeded);
        var roles = await userManager.GetRolesAsync(result.User!);
        Assert.Contains(RoleNames.ServerAdministrator, roles);
    }

    [Fact]
    public async Task CreateUser_MissingRoles_FailsAndCleansUp()
    {
        await using var testApp = await TestAppBuilder.CreateTestApp(output);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

        var tenant = new Tenant { Name = "Test Tenant" };
        appDb.Tenants.Add(tenant);
        await appDb.SaveChangesAsync();

        var missingRoleId = Guid.NewGuid();

        var result = await userCreator.CreateUser(
            "fail@example.com", 
            "Password123!", 
            tenant.Id, 
            roleIds: [missingRoleId]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.IdentityResult.Errors, e => e.Description.Contains("Roles not found"));

        // Verify user was deleted
        var user = await appDb.Users.FirstOrDefaultAsync(u => u.Email == "fail@example.com");
        Assert.Null(user);
    }

    [Fact]
    public async Task CreateUser_MissingTags_FailsAndCleansUp()
    {
        await using var testApp = await TestAppBuilder.CreateTestApp(output);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

        var tenant = new Tenant { Name = "Test Tenant" };
        appDb.Tenants.Add(tenant);
        await appDb.SaveChangesAsync();

        var missingTagId = Guid.NewGuid();

        var result = await userCreator.CreateUser(
            "failtags@example.com", 
            "Password123!", 
            tenant.Id, 
            tagIds: [missingTagId]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.IdentityResult.Errors, e => e.Description.Contains("Tags not found"));

        // Verify user was deleted
        var user = await appDb.Users.FirstOrDefaultAsync(u => u.Email == "failtags@example.com");
        Assert.Null(user);
    }

    [Fact]
    public async Task CreateUser_NewTenant_AssignsDefaultRoles()
    {
        var config = new Dictionary<string, string?>
        {
            ["AppOptions:DisableEmailSending"] = "true"
        };

        await using var testApp = await TestAppBuilder.CreateTestApp(output, extraConfiguration: config);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        // Create first user so next one isn't server admin
        await userCreator.CreateUser("admin@example.com", "Password123!", null);

        var result = await userCreator.CreateUser("tenantadmin@example.com", "Password123!", null);

        if (!result.Succeeded)
        {
             output.WriteLine($"CreateUser failed: {string.Join(", ", result.IdentityResult.Errors.Select(e => e.Description))}");
        }
        Assert.True(result.Succeeded);
        var roles = await userManager.GetRolesAsync(result.User!);
        Assert.Contains(RoleNames.TenantAdministrator, roles);
        Assert.Contains(RoleNames.DeviceSuperUser, roles);
        Assert.Contains(RoleNames.AgentInstaller, roles);
        Assert.DoesNotContain(RoleNames.ServerAdministrator, roles);
    }

    [Fact]
    public async Task CreateUser_WithExternalLogin_Succeeds()
    {
        await using var testApp = await TestAppBuilder.CreateTestApp(output);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();

        var email = "external@example.com";
        var loginInfo = new ExternalLoginInfo(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "123")], "TestAuth")),
            "TestProvider",
            "123",
            "Test User");

        var result = await userCreator.CreateUser(email, loginInfo, null);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.User);
        Assert.Equal(email, result.User.Email);
        
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var logins = await userManager.GetLoginsAsync(result.User);
        Assert.Contains(logins, l => l.LoginProvider == "TestProvider" && l.ProviderKey == "123");
    }
    [Fact]
    public async Task CreateUser_WithPassword_Succeeds()
    {
        await using var testApp = await TestAppBuilder.CreateTestApp(output);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();

        var email = "test@example.com";
        var result = await userCreator.CreateUser(email, "Password123!", null);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.User);
        Assert.Equal(email, result.User.Email);
        Assert.Equal(email, result.User.UserName);
    }

    [Fact]
    public async Task CreateUser_WithRolesAndTags_Succeeds()
    {
        await using var testApp = await TestAppBuilder.CreateTestApp(output);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();

        // Create tenant
        var tenant = new Tenant { Name = "Test Tenant" };
        appDb.Tenants.Add(tenant);
        
        // Create tags
        var tag1 = new Tag { Name = "Tag1", Tenant = tenant };
        var tag2 = new Tag { Name = "Tag2", Tenant = tenant };
        appDb.Tags.AddRange(tag1, tag2);
        await appDb.SaveChangesAsync();

        // Create custom role
        var role = new AppRole { Name = "CustomRole" };
        await roleManager.CreateAsync(role);

        var email = "complex@example.com";
        var result = await userCreator.CreateUser(
            email, 
            "Password123!", 
            tenant.Id, 
            roleIds: [role.Id], 
            tagIds: [tag1.Id, tag2.Id]);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.User);
        
        // Verify roles
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var userRoles = await userManager.GetRolesAsync(result.User);
        Assert.Contains("CustomRole", userRoles);

        // Verify tags
        // Need to reload user with tags
        var userWithTags = await appDb.Users.Include(u => u.Tags)
            .FirstOrDefaultAsync(u => u.Id == result.User.Id);
        Assert.NotNull(userWithTags);
        Assert.NotNull(userWithTags.Tags);
        Assert.Equal(2, userWithTags.Tags.Count);
        Assert.Contains(userWithTags.Tags, t => t.Name == "Tag1");
        Assert.Contains(userWithTags.Tags, t => t.Name == "Tag2");
    }

    [Fact]
    public async Task CreateUser_WithTenantId_Succeeds()
    {
        await using var testApp = await TestAppBuilder.CreateTestApp(output);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

        // Create a tenant first
        var tenant = new Tenant { Name = "Test Tenant" };
        appDb.Tenants.Add(tenant);
        await appDb.SaveChangesAsync();

        var email = "tenantuser@example.com";
        var result = await userCreator.CreateUser(email, "Password123!", tenant.Id);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.User);
        Assert.Equal(tenant.Id, result.User.TenantId);
    }
}